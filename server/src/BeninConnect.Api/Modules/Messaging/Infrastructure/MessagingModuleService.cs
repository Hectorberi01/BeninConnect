using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Messaging.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Infrastructure;

public sealed class MessagingModuleService(MessagingDbContext db) : IMessagingModule, IModuleStatistics
{
    public Task<int> CountMessagesAsync(CancellationToken ct) => db.Messages.CountAsync(ct);

    public async Task<IReadOnlyList<DateTimeOffset>> GetMessageDatesAsync(DateTimeOffset since, CancellationToken ct) =>
        await db.Messages.AsNoTracking().Where(m => m.SentAt >= since).Select(m => m.SentAt).ToListAsync(ct);

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) =>
    [
        new("Conversations", await db.Conversations.CountAsync(ct)),
        new("Messages", await db.Messages.CountAsync(ct))
    ];
}
