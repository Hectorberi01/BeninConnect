using BeninConnect.Modules.Feed.Application;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Feed.Infrastructure;

/// <summary>Photos et vidéos des publications.</summary>
public sealed class FeedMediaReferences(IFeedDb db) : IMediaReferences
{
    public async Task<IReadOnlyCollection<string>> ImagePathsAsync(CancellationToken ct)
    {
        var rows = await db.Posts.AsNoTracking().Select(p => new { p.ImagePath, p.VideoPath, p.Images }).ToListAsync(ct);
        return rows.SelectMany(r => r.Images.Append(r.ImagePath).Append(r.VideoPath)).OfType<string>().ToList();
    }
}
