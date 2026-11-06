# Remplacer les mocks par l'API ASP.NET Core 10

Les ViewModels ne dépendent que des interfaces de `Features/*/Contracts` et `Shared/Contracts`. Le passage à l'API se fait **sans toucher aux ViewModels ni aux vues**.

## 1. Interfaces à implémenter

| Interface | Mock actuel |
|---|---|
| `IAuthService` | `MockAuthService` |
| `IUserRepository` | `MockUserRepository` |
| `IPostRepository`, `ICommentRepository` | `MockPostRepository`, `MockCommentRepository` |
| `ICommunityRepository` | `MockCommunityRepository` |
| `IConversationRepository` | `MockConversationRepository` |
| `IEventRepository` | `MockEventRepository` |
| `INotificationRepository` | `MockNotificationRepository` |

Les modèles (`Post`, `Community`, `ConversationSummary`…) sont des records proches de DTO : ils peuvent être désérialisés directement ou mappés.

## 2. Exemple d'implémentation

```csharp
public sealed class ApiPostRepository(HttpClient http, IMessenger messenger) : IPostRepository
{
    public async Task<PagedResult<Post>> GetFeedAsync(Guid viewerId, int skip, int take, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<PagedResult<Post>>($"api/feed?skip={skip}&take={take}", ct)
        ?? new PagedResult<Post>([], 0, skip);

    public async Task ToggleLikeAsync(Guid postId, Guid userId, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"api/posts/{postId}/like", null, ct);
        response.EnsureSuccessStatusCode();
        var post = await response.Content.ReadFromJsonAsync<Post>(ct);
        messenger.Send(new PostChangedMessage(post!)); // garde les écrans synchronisés
    }
    // ...
}
```

**Important** : continuer à envoyer les messages (`PostChangedMessage`, `ConversationChangedMessage`…) après chaque écriture, sinon les autres écrans ne se mettent pas à jour. Pour le temps réel (nouveaux messages), un client SignalR peut publier les mêmes messages.

## 3. Enregistrement

Dans `Core/DependencyInjection/ServiceCollectionExtensions.AddDataSources` :

```csharp
if (!options.UseMockData)
{
    services.AddHttpClient<IPostRepository, ApiPostRepository>(c => c.BaseAddress = options.ApiBaseUrl);
    // ... une ligne par interface
    return services;
}
```

Ajouter le package `Microsoft.Extensions.Http`, puis passer `UseMockData = false` et `ApiBaseUrl` dans `App.Options` (par exemple depuis chaque hôte, selon la configuration Debug/Release).

## 4. Points d'attention

- **Authentification** : `IAuthService` devra stocker le jeton dans un stockage sécurisé de la plateforme (Keychain iOS, Keystore Android), pas dans `IPreferencesService` qui est en clair. Ajouter un `DelegatingHandler` qui injecte le jeton.
- **Erreurs** : convertir les erreurs HTTP métier en `DomainException` (message lisible) et les autres en exceptions génériques ; `ErrorMessages.From` les affiche déjà correctement.
- **Lots** : `GetByIdsAsync` (utilisateurs, communautés) est utilisé pour résoudre auteurs et communautés des cartes ; côté API, embarquer l'auteur dans la réponse ou exposer un endpoint de lot.
- **Thread UI** : ne pas utiliser `ConfigureAwait(false)` dans les repositories (les messages envoyés modifient des collections liées à l'UI), ou redispatcher sur `Dispatcher.UIThread`.
- **Tests** : les tests unitaires utilisent les mocks ; ajouter des tests de contrat par repository HTTP (serveur de test ASP.NET Core).
