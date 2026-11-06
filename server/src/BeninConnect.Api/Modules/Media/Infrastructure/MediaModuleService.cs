using BeninConnect.Modules.Media.Application;
using BeninConnect.Modules.Media.Contracts;

namespace BeninConnect.Modules.Media.Infrastructure;

public sealed class MediaModuleService(IMediaStorage storage) : IMediaModule
{
    public async Task<MediaUsage> UsageAsync(CancellationToken ct)
    {
        var (files, bytes) = await storage.UsageAsync(ct);
        return new MediaUsage(files, bytes);
    }
}
