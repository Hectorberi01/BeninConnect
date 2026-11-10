using BeninConnect.Modules.Notifications.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Notifications.Application;

public interface INotificationsDb
{
    DbSet<Notification> Notifications { get; }
    DbSet<PushDevice> PushDevices { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record NotificationDto(
    Guid Id, Guid UserId, NotificationCategory Category, string Title, string Body, DateTimeOffset CreatedAt,
    bool IsRead, NotificationTarget Target, Guid? TargetId);

public static class NotificationMapping
{
    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.UserId, n.Category, n.Title, n.Body, n.CreatedAt, n.IsRead, n.Target, n.TargetId);
}

public sealed record GetNotificationsQuery(NotificationCategory? Category) : IQuery<IReadOnlyList<NotificationDto>>;

public sealed record GetUnreadNotificationCountQuery : IQuery<CountDto>;

public sealed record MarkNotificationReadCommand(Guid Id) : ICommand<NotificationDto>;

public sealed record MarkAllNotificationsReadCommand : ICommand<CountDto>;

/// <summary>Supprime une de mes notifications.</summary>
public sealed record DeleteNotificationCommand(Guid Id) : ICommand<CountDto>;

/// <summary>Supprime toutes mes notifications (ou celles d'une catégorie).</summary>
public sealed record ClearNotificationsCommand(NotificationCategory? Category) : ICommand<CountDto>;

public sealed class NotificationHandlers(INotificationsDb db, ICurrentUser me) :
    IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationDto>>,
    IRequestHandler<GetUnreadNotificationCountQuery, CountDto>,
    IRequestHandler<MarkNotificationReadCommand, NotificationDto>,
    IRequestHandler<MarkAllNotificationsReadCommand, CountDto>,
    IRequestHandler<DeleteNotificationCommand, CountDto>,
    IRequestHandler<ClearNotificationsCommand, CountDto>
{
    public async Task<IReadOnlyList<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == me.Id);
        if (request.Category is { } category)
        {
            query = query.Where(n => n.Category == category);
        }

        return (await query.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync(ct)).Select(n => n.ToDto()).ToList();
    }

    public async Task<CountDto> Handle(GetUnreadNotificationCountQuery request, CancellationToken ct) =>
        new(await db.Notifications.CountAsync(n => n.UserId == me.Id && !n.IsRead, ct));

    public async Task<NotificationDto> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == request.Id && n.UserId == me.Id, ct)
                           ?? throw new NotFoundException("Notification");
        notification.MarkRead();
        await db.SaveChangesAsync(ct);
        return notification.ToDto();
    }

    public async Task<CountDto> Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        var unread = await db.Notifications.Where(n => n.UserId == me.Id && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread)
        {
            n.MarkRead();
        }

        await db.SaveChangesAsync(ct);
        return new CountDto(unread.Count);
    }

    public async Task<CountDto> Handle(DeleteNotificationCommand request, CancellationToken ct)
    {
        var deleted = await db.Notifications.Where(n => n.Id == request.Id && n.UserId == me.Id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? throw new NotFoundException("Notification") : new CountDto(deleted);
    }

    public async Task<CountDto> Handle(ClearNotificationsCommand request, CancellationToken ct)
    {
        var query = db.Notifications.Where(n => n.UserId == me.Id);
        if (request.Category is { } category)
        {
            query = query.Where(n => n.Category == category);
        }

        return new CountDto(await query.ExecuteDeleteAsync(ct));
    }
}
