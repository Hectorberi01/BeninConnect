using BeninConnect.Modules.Administration.Application;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Administration.Infrastructure;

/// <summary>Photos des bannières de l'accueil.</summary>
public sealed class AdministrationMediaReferences(IAdministrationDb db) : IMediaReferences
{
    public async Task<IReadOnlyCollection<string>> ImagePathsAsync(CancellationToken ct) =>
        await db.Banners.AsNoTracking().Where(b => b.ImagePath != null).Select(b => b.ImagePath!).ToListAsync(ct);
}
