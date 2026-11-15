using BeninConnect.Modules.Media.Application;
using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Media.Infrastructure;

/// <summary>Section « Media:OrphanCleanup ».</summary>
public sealed class OrphanCleanupSettings
{
    public const string Section = "Media:OrphanCleanup";

    /// <summary>false (défaut) : rapport seulement, aucun fichier effacé.</summary>
    public bool Delete { get; set; }

    /// <summary>Fichiers récents ignorés (envoyés mais pas encore enregistrés dans une publication).</summary>
    public int GraceDays { get; set; } = 7;

    public int MaxDeletesPerRun { get; set; } = 500;
}

/// <summary>
/// Photos et vidéos publiques que plus aucun module n'utilise (IMediaReferences de chaque module).
/// Rapport par défaut ; suppression seulement si Media:OrphanCleanup:Delete.
/// </summary>
public sealed class MediaOrphansTask(
    IMediaStorage storage, IEnumerable<IMediaReferences> sources, OrphanCleanupSettings settings, TimeProvider clock, ILogger<MediaOrphansTask> log)
    : IScheduledTask
{
    public bool Enabled => true;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            foreach (var path in await source.ImagePathsAsync(ct))
            {
                used.Add(FileName(path));
            }
        }

        var limit = clock.GetUtcNow().AddDays(-Math.Max(1, settings.GraceDays));
        var orphans = new List<StoredFile>();
        await foreach (var file in storage.ListAsync(ct))
        {
            if (file.LastModified < limit && !used.Contains(file.Name))
            {
                orphans.Add(file);
            }
        }

        var size = $"{orphans.Count} fichier(s) orphelin(s), {orphans.Sum(f => f.Size) / (1024 * 1024)} Mo";
        if (!settings.Delete)
        {
            return $"{size} (rapport seulement : Media__OrphanCleanup__Delete=true pour supprimer).";
        }

        var deleted = 0;
        foreach (var file in orphans.OrderBy(f => f.LastModified).Take(Math.Max(1, settings.MaxDeletesPerRun)))
        {
            await storage.DeleteAsync(file.Name, ct);
            deleted++;
        }

        log.LogInformation("Médias : {Count} fichier(s) orphelin(s) supprimé(s)", deleted);
        return $"{size}, {deleted} supprimé(s).";
    }

    /// <summary>« uploads/a.jpg », « /uploads/a.jpg » ou URL complète → « a.jpg ».</summary>
    internal static string FileName(string path)
    {
        var clean = path.Split('?', '#')[0];
        return clean[(clean.LastIndexOf('/') + 1)..];
    }
}
