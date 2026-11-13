using BeninConnect.Modules.Messaging.Application;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Messaging.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Infrastructure;

public sealed class MessagingModuleService(MessagingDbContext db, DirectMessages direct) : IMessagingModule, IModuleStatistics
{
    public Task<Guid> SendDirectAsync(Guid senderId, Guid recipientId, string content, bool notifyRecipient, CancellationToken ct) =>
        direct.SendAsync(senderId, recipientId, content, notifyRecipient, ct);

    public Task<int> CountMessagesAsync(CancellationToken ct) => db.Messages.CountAsync(ct);

    public async Task<MemberMessagingStats> GetMemberStatsAsync(Guid userId, CancellationToken ct)
    {
        var sent = db.Messages.AsNoTracking().Where(m => m.SenderId == userId);
        return new MemberMessagingStats(
            await db.Conversations.CountAsync(c => c.Participants.Any(p => p.UserId == userId), ct),
            await sent.CountAsync(ct),
            await sent.MaxAsync(m => (DateTimeOffset?)m.SentAt, ct));
    }

    public async Task<IReadOnlyList<DateTimeOffset>> GetMessageDatesAsync(DateTimeOffset since, CancellationToken ct) =>
        await db.Messages.AsNoTracking().Where(m => m.SentAt >= since).Select(m => m.SentAt).ToListAsync(ct);

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) =>
    [
        new("Conversations", await db.Conversations.CountAsync(ct)),
        new("Messages", await db.Messages.CountAsync(ct))
    ];
}
