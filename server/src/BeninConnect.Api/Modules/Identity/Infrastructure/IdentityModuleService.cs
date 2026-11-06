using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Identity.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Identity.Infrastructure;

/// <summary>Implémentation du contrat public (appel direct en monolithe, client HTTP/gRPC après découpage).</summary>
public sealed class IdentityModuleService(IdentityDbContext db, TimeProvider clock) : IIdentityModule, IModuleStatistics
{
    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => db.Users.AnyAsync(u => u.Id == userId, ct);

    public async Task<IReadOnlyDictionary<Guid, UserDto>> GetUsersAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return (await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToListAsync(ct)).ToDictionary(u => u.Id, u => u.ToDto());
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => list.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    public async Task<string> SuspendAsync(Guid userId, string reason, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("Membre");
        user.Suspend(reason, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return user.DisplayName;
    }

    public async Task<MemberStats> GetStatsAsync(DateTimeOffset since, CancellationToken ct)
    {
        var countries = (await db.Users.AsNoTracking()
                .GroupBy(u => u.Country).Select(g => new { g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(8).ToListAsync(ct))
            .Select(x => new LabelCount(x.Key, x.Count)).ToList();

        return new MemberStats(
            await db.Users.CountAsync(ct),
            await db.Users.CountAsync(u => u.IsEmailVerified, ct),
            await db.Users.CountAsync(u => u.IsSuspended, ct),
            await GetJoinDatesAsync(since, ct),
            countries);
    }

    public async Task<IReadOnlyList<DateTimeOffset>> GetJoinDatesAsync(DateTimeOffset since, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.JoinedAt >= since).Select(u => u.JoinedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<MemberExportRow>> GetExportRowsAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().OrderBy(u => u.JoinedAt)
            .Select(u => new MemberExportRow(u.Id, u.FirstName, u.LastName, u.Email, u.Country, u.City, u.JoinedAt, u.IsEmailVerified, u.IsSuspended))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) => [new("Membres", await db.Users.CountAsync(ct))];
}
