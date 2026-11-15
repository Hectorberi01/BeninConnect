using BeninConnect.Modules.Communities.Application;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Communities.Infrastructure;

/// <summary>Couvertures des communautés.</summary>
public sealed class CommunitiesMediaReferences(ICommunitiesDb db) : IMediaReferences
{
    public async Task<IReadOnlyCollection<string>> ImagePathsAsync(CancellationToken ct)
    {
        return await db.Communities.AsNoTracking().Where(c => c.CoverImage != null).Select(c => c.CoverImage!).ToListAsync(ct);
    }
}
