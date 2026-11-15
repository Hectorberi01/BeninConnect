using BeninConnect.Modules.AppVersionManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.AppVersionManagement.Application;

public interface IAppVersionsDb
{
    DbSet<AppInstallation> Installations { get; }
    DbSet<AppRelease> Releases { get; }
    DbSet<UpdateCampaign> Campaigns { get; }
    DbSet<CampaignRecipient> Recipients { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Section « AppVersions ».</summary>
public sealed class AppVersionSettings
{
    public const string Section = "AppVersions";

    public string AppCode { get; set; } = "beninconnect";
    public string AppName { get; set; } = "BéninConnect";

    /// <summary>Installations vues depuis ce délai : comptées dans les statistiques et les relances.</summary>
    public int ActiveDays { get; set; } = 90;

    /// <summary>Un membre relancé ne l'est pas de nouveau avant ce délai.</summary>
    public int NotifyEveryDays { get; set; } = 7;

    /// <summary>Installations sans nouvelle depuis ce délai supprimées.</summary>
    public int ForgetAfterDays { get; set; } = 365;

    public int SendBatchSize { get; set; } = 200;

    public Dictionary<string, string> StoreUrls { get; set; } = new()
    {
        ["ios"] = "https://apps.apple.com/app/id6832112483",
        ["android"] = "https://play.google.com/store/apps/details?id=com.hbatech.beninconnect.rn",
    };

    /// <summary>Ancien réglage MobileApp:MinimumVersion : minimum commun, ajouté à celui saisi dans la console (le plus élevé l'emporte).</summary>
    public string? LegacyMinimumVersion { get; set; }
}

/// <summary>Version de référence d'une plateforme (enregistrée ou par défaut).</summary>
public sealed record ReleaseInfo(string Platform, string? LatestVersion, string? MinimumSupportedVersion, string StoreUrl, DateTimeOffset? UpdatedAt, string? UpdatedBy)
{
    public UpdateStatus StatusOf(string version) => VersionRules.StatusOf(version, LatestVersion, MinimumSupportedVersion);

    /// <summary>Version à installer : la dernière, sinon le minimum.</summary>
    public string? Target => LatestVersion ?? MinimumSupportedVersion;
}

public static class AppVersionsData
{
    public static async Task<IReadOnlyDictionary<string, ReleaseInfo>> ReleasesAsync(IAppVersionsDb db, AppVersionSettings settings, CancellationToken ct)
    {
        var saved = await db.Releases.AsNoTracking().Where(r => r.AppCode == settings.AppCode).ToListAsync(ct);
        var legacy = VersionRules.IsVersion(settings.LegacyMinimumVersion?.Trim()) ? settings.LegacyMinimumVersion!.Trim() : null;
        return VersionRules.Platforms.ToDictionary(p => p, p => saved.FirstOrDefault(r => r.Platform == p) is { } r
            ? new ReleaseInfo(p, r.LatestVersion, Highest(r.MinimumSupportedVersion, legacy), r.StoreUrl, r.UpdatedAt, r.UpdatedBy)
            : new ReleaseInfo(p, null, legacy, settings.StoreUrls.GetValueOrDefault(p, ""), null, null));
    }

    /// <summary>MobileApp:MinimumVersion reste un plancher commun aux deux plateformes.</summary>
    private static string? Highest(string? a, string? b) => a is null ? b : b is null ? a : VersionRules.Compare(a, b) >= 0 ? a : b;

    /// <summary>« UP_TO_DATE », « UPDATE_AVAILABLE », « UPDATE_REQUIRED », « UNKNOWN » (réponse de l'application).</summary>
    public static string Code(UpdateStatus status) => status switch
    {
        UpdateStatus.UpToDate => "UP_TO_DATE",
        UpdateStatus.UpdateAvailable => "UPDATE_AVAILABLE",
        UpdateStatus.UpdateRequired => "UPDATE_REQUIRED",
        _ => "UNKNOWN",
    };

    /// <summary>Installation la plus récente de chaque membre et plateforme (une réinstallation remplace l'ancienne).</summary>
    public static IEnumerable<T> LatestPerUserAndPlatform<T>(IEnumerable<T> rows, Func<T, Guid> user, Func<T, string> platform, Func<T, DateTimeOffset> seen) =>
        rows.GroupBy(r => (user(r), platform(r))).Select(g => g.MaxBy(seen)!);
}
