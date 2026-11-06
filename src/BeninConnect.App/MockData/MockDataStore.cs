using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Events.Models;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Messaging.Models;
using BeninConnect.App.Features.Notifications.Models;
using BeninConnect.App.Shared.Models;

namespace BeninConnect.App.MockData;

/// <summary>
/// État applicatif partagé par tous les repositories mockés (singleton).
/// Les ViewModels n'y accèdent jamais directement : ils passent par les interfaces de repository.
/// Les modèles étant des records immuables, une modification remplace l'élément dans la liste.
/// </summary>
public sealed class MockDataStore
{
    public object SyncRoot { get; } = new();

    public List<User> Users { get; } = [];
    public List<Community> Communities { get; } = [];
    public List<JoinRequest> JoinRequests { get; } = [];
    public List<Post> Posts { get; } = [];
    public List<Comment> Comments { get; } = [];
    public List<Conversation> Conversations { get; } = [];
    public List<Message> Messages { get; } = [];
    public List<CommunityEvent> Events { get; } = [];
    public List<Notification> Notifications { get; } = [];

    /// <summary>Identifiants de démonstration uniquement (email → mot de passe fictif). Jamais persistés.</summary>
    public Dictionary<string, string> DemoCredentials { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Comptes créés dans la session et en attente de vérification email.</summary>
    public HashSet<string> PendingVerifications { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Guid DemoUserId { get; set; }

    /// <summary>Remplace l'élément correspondant et retourne la nouvelle version.</summary>
    public static T Replace<T>(List<T> list, Predicate<T> match, Func<T, T> update, string what)
    {
        var index = list.FindIndex(match);
        if (index < 0)
        {
            throw new Core.Utilities.NotFoundException(what);
        }

        var updated = update(list[index]);
        list[index] = updated;
        return updated;
    }
}
