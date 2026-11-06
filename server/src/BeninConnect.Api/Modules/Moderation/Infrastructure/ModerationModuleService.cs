using BeninConnect.Modules.Moderation.Contracts;
using BeninConnect.Modules.Moderation.Domain;
using BeninConnect.Modules.Moderation.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Moderation.Infrastructure;

public sealed class ModerationModuleService(ModerationDbContext db) : IModerationModule, IModuleStatistics
{
    public async Task<IReadOnlyDictionary<Guid, int>> CountReportsAsync(ReportTarget target, IReadOnlyCollection<Guid> targetIds, CancellationToken ct) =>
        await db.Reports.AsNoTracking().Where(r => r.TargetType == target && targetIds.Contains(r.TargetId))
            .GroupBy(r => r.TargetId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<IReadOnlyList<Guid>> GetOpenTargetIdsAsync(ReportTarget target, CancellationToken ct) =>
        await db.Reports.AsNoTracking().Where(r => r.TargetType == target && r.Status == ReportStatus.Open).Select(r => r.TargetId).Distinct().ToListAsync(ct);

    public Task<int> CountOpenAsync(CancellationToken ct) => db.Reports.CountAsync(r => r.Status == ReportStatus.Open, ct);

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) => [new("Signalements", await db.Reports.CountAsync(ct))];
}
