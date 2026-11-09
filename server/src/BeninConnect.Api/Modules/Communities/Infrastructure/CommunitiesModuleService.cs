using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Communities.Domain;
using BeninConnect.Modules.Communities.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Communities.Infrastructure;

public sealed class CommunitiesModuleService(CommunitiesDbContext db, IEventBus bus) : ICommunitiesModule, IModuleStatistics
{
    public async Task<IReadOnlyList<Guid>> GetCommunityIdsOfMemberAsync(Guid userId, CancellationToken ct) =>
        await db.Communities.AsNoTracking().Where(c => c.Members.Any(m => m.UserId == userId)).Select(c => c.Id).ToListAsync(ct);

    public Task<bool> IsMemberAsync(Guid communityId, Guid userId, CancellationToken ct) =>
        db.Communities.AnyAsync(c => c.Id == communityId && c.Members.Any(m => m.UserId == userId), ct);

    public async Task<IReadOnlyDictionary<Guid, CommunityInfo>> GetInfosAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Communities.AsNoTracking().Where(c => list.Contains(c.Id))
            .Select(c => new CommunityInfo(c.Id, c.Name, c.OwnerId)).ToDictionaryAsync(c => c.Id, ct);
    }

    public async Task<IReadOnlyList<string>> GetCommunityNamesOfMemberAsync(Guid userId, CancellationToken ct) =>
        await db.Communities.AsNoTracking().Where(c => c.Members.Any(m => m.UserId == userId)).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<MemberCommunity>> GetMembershipsAsync(Guid userId, CancellationToken ct) =>
        (await db.Communities.AsNoTracking().Include(c => c.Members).Where(c => c.Members.Any(m => m.UserId == userId)).ToListAsync(ct))
        .Select(c => (c, m: c.Members.First(m => m.UserId == userId)))
        .Select(x => new MemberCommunity(x.c.Id, x.c.Name, x.m.Role.ToString(), x.c.IsPrivate, x.m.JoinedAt))
        .OrderByDescending(m => m.JoinedAt).ToList();

    public async Task<IReadOnlyDictionary<Guid, int>> CountMembershipsByUserAsync(CancellationToken ct) =>
        await db.Communities.AsNoTracking().SelectMany(c => c.Members).GroupBy(m => m.UserId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<string> DeleteAsync(Guid communityId, CancellationToken ct)
    {
        var community = await db.Communities.FirstOrDefaultAsync(c => c.Id == communityId, ct) ?? throw new NotFoundException("Communauté");
        db.Communities.Remove(community);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new CommunityDeleted(communityId), ct);
        return community.Name;
    }

    public async Task<CommunityStats> GetStatsAsync(CancellationToken ct)
    {
        var top = await db.Communities.AsNoTracking()
            .OrderByDescending(c => c.Members.Count).Take(5)
            .Select(c => new LabelCount(c.Name, c.Members.Count)).ToListAsync(ct);
        return new CommunityStats(
            await db.Communities.CountAsync(ct),
            await db.Communities.CountAsync(c => c.Visibility == CommunityVisibility.Private, ct),
            top);
    }

    public async Task<IReadOnlyList<CommunityExportRow>> GetExportRowsAsync(CancellationToken ct) =>
        (await db.Communities.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Category, c.Country, c.City, c.Visibility, Members = c.Members.Count, c.CreatedAt })
            .ToListAsync(ct))
        .Select(c => new CommunityExportRow(c.Id, c.Name, c.Category, c.Country, c.City, c.Visibility.ToString(), c.Members, c.CreatedAt))
        .ToList();

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) => [new("Communautés", await db.Communities.CountAsync(ct))];
}
