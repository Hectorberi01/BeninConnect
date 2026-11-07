using BeninConnect.Modules.Notifications.Application;
using BeninConnect.Modules.Notifications.Domain;
using BeninConnect.Modules.Notifications.Infrastructure;
using BeninConnect.Modules.Notifications.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Notifications.Api;

public sealed class NotificationsModule : IModule
{
    public string Name => "Notifications";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<NotificationsDbContext>(builder.Configuration, Name, NotificationsDbContext.SchemaName);
        services.AddScoped<INotificationsDb>(sp => sp.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<NotificationsDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Notifications");

        // Notifications push : file en mémoire + envoi en arrière-plan via Expo (APNs / FCM)
        var push = builder.Configuration.GetSection(PushSettings.Section).Get<PushSettings>() ?? new PushSettings();
        services.AddSingleton(push);
        services.AddSingleton<ChannelPushQueue>();
        services.AddSingleton<IPushQueue>(sp => sp.GetRequiredService<ChannelPushQueue>());
        services.AddHostedService<PushDispatcher>();
        services.AddScoped<PushComposer>();
        if (push.IsExpo)
        {
            services.AddHttpClient<IPushSender, ExpoPushSender>(c =>
            {
                c.BaseAddress = new Uri(ExpoPushSender.BaseAddress);
                c.Timeout = TimeSpan.FromSeconds(15);
            });
        }
        else
        {
            services.AddScoped<IPushSender, LogPushSender>();
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var notifications = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();
        notifications.MapGet("", (NotificationCategory? category, ISender s, CancellationToken ct) => s.Send(new GetNotificationsQuery(category), ct));
        notifications.MapGet("/unread-count", (ISender s, CancellationToken ct) => s.Send(new GetUnreadNotificationCountQuery(), ct));
        notifications.MapPost("/{id:guid}/read", (Guid id, ISender s, CancellationToken ct) => s.Send(new MarkNotificationReadCommand(id), ct));
        notifications.MapPost("/read-all", (ISender s, CancellationToken ct) => s.Send(new MarkAllNotificationsReadCommand(), ct));

        // Téléphone du membre : enregistré après la connexion et à chaque changement de réglages, retiré à la déconnexion.
        notifications.MapPost("/devices", async (RegisterPushDeviceCommand c, ISender s, CancellationToken ct) =>
        {
            await s.Send(c, ct);
            return Results.NoContent();
        });
        notifications.MapPost("/devices/unregister", async (UnregisterPushDeviceCommand c, ISender s, CancellationToken ct) =>
        {
            await s.Send(c, ct);
            return Results.NoContent();
        });
    }
}
