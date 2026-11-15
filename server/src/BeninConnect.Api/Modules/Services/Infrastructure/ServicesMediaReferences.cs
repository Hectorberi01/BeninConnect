using BeninConnect.Modules.Services.Application;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Services.Infrastructure;

/// <summary>Photos des prestataires et des services proposés.</summary>
public sealed class ServicesMediaReferences(IServicesDb db) : IMediaReferences
{
    public async Task<IReadOnlyCollection<string>> ImagePathsAsync(CancellationToken ct)
    {
        var photos = await db.Providers.AsNoTracking().Where(p => p.PhotoPath != null).Select(p => p.PhotoPath!).ToListAsync(ct);
        var images = await db.Offerings.AsNoTracking().Select(o => o.Images).ToListAsync(ct);
        return photos.Concat(images.SelectMany(i => i)).ToList();
    }
}
