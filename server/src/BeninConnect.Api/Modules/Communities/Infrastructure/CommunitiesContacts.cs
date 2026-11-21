using BeninConnect.Modules.Communities.Application;
using BeninConnect.Modules.Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Communities.Infrastructure;

/// <summary>Profil privé : les membres d'une même communauté sont des contacts.</summary>
public sealed class CommunitiesContacts(ICommunitiesDb db) : IContactDirectory
{
    public async Task<IReadOnlySet<Guid>> ContactsAmongAsync(Guid viewerId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.ToList();
        var shared = await db.Communities.AsNoTracking()
            .Where(c => c.Members.Any(m => m.UserId == viewerId))
            .SelectMany(c => c.Members).Where(m => ids.Contains(m.UserId)).Select(m => m.UserId).Distinct().ToListAsync(ct);
        return shared.ToHashSet();
    }
}
