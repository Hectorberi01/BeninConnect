using BeninConnect.Modules.Events.Contracts;
using BeninConnect.Modules.Events.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Events.Infrastructure;

public sealed class EventsModuleService(EventsDbContext db) : IEventsModule, IModuleStatistics
{
    public async Task<EventStats> GetStatsAsync(DateTimeOffset now, CancellationToken ct) => new(
        await db.Events.CountAsync(e => e.EndsAt >= now, ct),
        await db.Events.SelectMany(e => e.Participants).CountAsync(ct));

    public async Task<IReadOnlyList<MemberEvent>> GetMemberEventsAsync(Guid userId, int take, CancellationToken ct) =>
        await db.Events.AsNoTracking()
            .Where(e => e.OrganizerId == userId || e.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(e => e.StartsAt).Take(take)
            .Select(e => new MemberEvent(e.Id, e.Title, e.City, e.StartsAt, e.OrganizerId == userId, e.Participants.Count))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountByCommunityAsync(CancellationToken ct) =>
        await db.Events.AsNoTracking().Where(e => e.CommunityId != null).GroupBy(e => e.CommunityId!.Value)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<IReadOnlyList<EventExportRow>> GetExportRowsAsync(CancellationToken ct) =>
        await db.Events.AsNoTracking().OrderBy(e => e.StartsAt)
            .Select(e => new EventExportRow(e.Title, e.Category, e.City, e.Country, e.StartsAt, e.EndsAt, e.Capacity, e.Participants.Count))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) => [new("Événements", await db.Events.CountAsync(ct))];
}
