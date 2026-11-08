using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Media.Contracts;

public sealed record MediaUsage(long Files, long Bytes);

public interface IMediaModule
{
    /// <summary>Nombre de photos envoyées et volume total (page Système de la console).</summary>
    Task<MediaUsage> UsageAsync(CancellationToken ct);
}

/// <summary>Photos qui ne sont plus utilisées (publication ou compte supprimé) : effacées du stockage.</summary>
public sealed record ImagesReleased(IReadOnlyList<string> Paths) : IIntegrationEvent;
