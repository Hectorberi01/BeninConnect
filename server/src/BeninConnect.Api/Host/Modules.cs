using BeninConnect.Modules.Administration.Api;
using BeninConnect.Modules.Communities.Api;
using BeninConnect.Modules.Editorial.Api;
using BeninConnect.Modules.Events.Api;
using BeninConnect.Modules.Feed.Api;
using BeninConnect.Modules.Identity.Api;
using BeninConnect.Modules.Jobs.Api;
using BeninConnect.Modules.Media.Api;
using BeninConnect.Modules.Messaging.Api;
using BeninConnect.Modules.Moderation.Api;
using BeninConnect.Modules.Notifications.Api;
using BeninConnect.Modules.Services.Api;
using BeninConnect.Shared.Api;

namespace BeninConnect.Host;

/// <summary>Modules du monolithe. Retirer un module d'ici (et le déployer à part) est la première étape du découpage.</summary>
public static class ModuleCatalog
{
    public static readonly IReadOnlyList<IModule> All =
    [
        new IdentityModule(),
        new CommunitiesModule(),
        new FeedModule(),
        new EventsModule(),
        new MessagingModule(),
        new NotificationsModule(),
        new ModerationModule(),
        new AdministrationModule(),
        new MediaModule(),
        new EditorialModule(),
        new JobsModule(),
        new ServicesModule()
    ];
}
