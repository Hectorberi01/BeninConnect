using BeninConnect.Modules.Notifications.Application;
using BeninConnect.Modules.Notifications.Domain;
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
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var notifications = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();
        notifications.MapGet("", (NotificationCategory? category, ISender s, CancellationToken ct) => s.Send(new GetNotificationsQuery(category), ct));
        notifications.MapGet("/unread-count", (ISender s, CancellationToken ct) => s.Send(new GetUnreadNotificationCountQuery(), ct));
        notifications.MapPost("/{id:guid}/read", (Guid id, ISender s, CancellationToken ct) => s.Send(new MarkNotificationReadCommand(id), ct));
        notifications.MapPost("/read-all", (ISender s, CancellationToken ct) => s.Send(new MarkAllNotificationsReadCommand(), ct));
    }
}
