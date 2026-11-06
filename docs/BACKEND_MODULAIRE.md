# Backend : monolithe modulaire

Un seul projet, `server/src/BeninConnect.Api`, déployé en un seul conteneur. Le code est découpé en modules
indépendants, chacun en Clean Architecture, pour pouvoir en extraire un en service séparé sans réécriture.

```
BeninConnect.Api/
├── Program.cs                 composition : noyau commun + modules
├── Host/                      configuration de prod, migrations au démarrage, jeu de démonstration
├── Shared/                    noyau commun (aucune dépendance vers un module)
│   ├── Domain/                exceptions métier, réactions
│   ├── Application/           médiateur CQRS, validation, bus d'événements (interfaces), pagination
│   ├── Infrastructure/        DbContext de base, bus en mémoire, JWT, hachage des mots de passe
│   └── Api/                   erreurs → ProblemDetails, utilisateur courant, IModule
└── Modules/
    └── <Module>/
        ├── Contracts/         SEULE partie visible des autres modules : interface I<Module>Module, DTO, événements
        ├── Domain/            entités et règles métier (aucun framework)
        ├── Application/       commandes, requêtes, handlers, abonnés aux événements
        ├── Infrastructure/    DbContext du module, migrations, implémentation du contrat
        └── Api/               <Module>Module : services + routes HTTP
```

## Modules

| Module | Schéma | Contenu | Utilise (contrats) |
|---|---|---|---|
| Identity | `identity` | inscription, connexion, profil, emails, suspension | Feed, Communities, Moderation (console) |
| Communities | `communities` | communautés, membres, demandes d'adhésion | Identity, Feed (console) |
| Feed | `feed` | publications, commentaires, j'aime, réactions | Communities, Identity, Moderation |
| Events | `events` | événements, participations | Communities, Identity |
| Messaging | `messaging` | conversations, messages, réactions | Identity |
| Notifications | `notifications` | notifications des membres | Identity ; écoute les événements des autres |
| Moderation | `moderation` | signalements, file de modération | Feed, Communities, Identity |
| Administration | `administration` | comptes admin, rôles, audit, tableau de bord, exports | tous (lecture via contrats) |
| Media | — | photos (disque ou S3) | — |

Chaque module expose aussi ses routes de console (`/api/admin/posts` dans Feed, `/api/admin/members` dans Identity…),
protégées par les permissions déclarées dans `Administration/Contracts`.

## Règles (vérifiées par `ArchitectureTests`)

- Un module n'utilise des autres modules QUE leurs `Contracts`.
- Contracts ← Domain ← Application ← Infrastructure / Api, jamais l'inverse.
- `Shared` ne connaît aucun module. Seul `Host` connaît tous les modules (composition, jeu de démonstration).
- Un DbContext et un schéma PostgreSQL par module, aucune clé étrangère entre modules (seulement des identifiants).

## Communication entre modules

- **Lecture / action synchrone** : interface du contrat (`IIdentityModule.GetNamesAsync`, `IFeedModule.HidePostAsync`…),
  implémentée dans l'Infrastructure du module. Après découpage : client HTTP ou gRPC avec la même interface.
- **Faits** : événements d'intégration publiés après enregistrement (`PostLiked`, `CommunityDeleted`, `MessageSent`…)
  via `IEventBus`. Aujourd'hui bus en mémoire dans la requête ; après découpage : RabbitMQ / Azure Service Bus,
  idéalement avec une table outbox par module.

## Découper un module plus tard

1. Copier `Modules/<Module>` + `Shared` dans un nouveau service et y mettre `Program.cs` qui n'enregistre que ce module.
2. Donner au module sa propre base : `ConnectionStrings__<Module>` (déjà lu en priorité par `AddModuleDbContext`).
3. Dans le monolithe, remplacer l'implémentation du contrat par un client HTTP, et le bus en mémoire par un broker.
4. Retirer le module de `Host/Modules.cs`.

## Ajouter une migration

```bash
./tools/ef-migrations.sh NomDeLaMigration Feed
```

## Tests sans Docker

`BC_TEST_POSTGRES="Host=localhost;Username=postgres;Password=postgres" dotnet test server/tests/BeninConnect.Api.Tests`
utilise un PostgreSQL existant (une base neuve par exécution) au lieu de Testcontainers.
