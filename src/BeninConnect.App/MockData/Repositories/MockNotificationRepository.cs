using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Notifications.Contracts;
using BeninConnect.App.Features.Notifications.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.MockData.Repositories;

public sealed class MockNotificationRepository(MockDataStore store, IMessenger messenger, DeveloperOptions developer, TimeProvider clock)
    : MockRepositoryBase(store, messenger, developer, clock), INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, NotificationCategory? category = null, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Notifications
                .Where(n => n.UserId == userId && (category is null || n.Category == category))
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Notifications.Count(n => n.UserId == userId && !n.IsRead);
        }
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        Notification updated;
        lock (Store.SyncRoot)
        {
            updated = MockDataStore.Replace(Store.Notifications, n => n.Id == notificationId, n => n with { IsRead = true }, "Notification");
        }

        Messenger.Send(new NotificationsChangedMessage(updated.UserId));
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            for (var i = 0; i < Store.Notifications.Count; i++)
            {
                var n = Store.Notifications[i];
                if (n.UserId == userId && !n.IsRead)
                {
                    Store.Notifications[i] = n with { IsRead = true };
                }
            }
        }

        Messenger.Send(new NotificationsChangedMessage(userId));
    }
}
