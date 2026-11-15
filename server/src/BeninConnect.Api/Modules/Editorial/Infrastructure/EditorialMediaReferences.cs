using BeninConnect.Modules.Editorial.Application;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Editorial.Infrastructure;

/// <summary>Images des contenus collectés ou rédigés.</summary>
public sealed class EditorialMediaReferences(IEditorialDb db) : IMediaReferences
{
    public async Task<IReadOnlyCollection<string>> ImagePathsAsync(CancellationToken ct)
    {
        var images = await db.Candidates.AsNoTracking().Select(c => c.Images).ToListAsync(ct);
        return images.SelectMany(i => i).ToList();
    }
}
