using BeninConnect.App.Features.Notifications.Models;

namespace BeninConnect.App.Features.Notifications.Contracts;

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, NotificationCategory? category = null, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
