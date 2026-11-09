using BeninConnect.Modules.Notifications.Contracts;
using BeninConnect.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Notifications.Infrastructure;

public sealed class NotificationsModuleService(NotificationsDbContext db) : INotificationsModule
{
    public async Task<IReadOnlyList<MemberDevice>> GetDevicesAsync(Guid userId, CancellationToken ct) =>
        await db.PushDevices.AsNoTracking().Where(d => d.UserId == userId).OrderByDescending(d => d.LastSeenAt)
            .Select(d => new MemberDevice(d.Platform, d.RegisteredAt, d.LastSeenAt)).ToListAsync(ct);
}
