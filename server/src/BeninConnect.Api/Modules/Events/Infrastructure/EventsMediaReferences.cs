using BeninConnect.Modules.Events.Application;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Events.Infrastructure;

/// <summary>Images des événements.</summary>
public sealed class EventsMediaReferences(IEventsDb db) : IMediaReferences
{
    public async Task<IReadOnlyCollection<string>> ImagePathsAsync(CancellationToken ct)
    {
        return await db.Events.AsNoTracking().Where(e => e.ImagePath != null).Select(e => e.ImagePath!).ToListAsync(ct);
    }
}
