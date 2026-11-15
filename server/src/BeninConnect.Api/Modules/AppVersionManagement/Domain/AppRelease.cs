using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.AppVersionManagement.Domain;

/// <summary>Version de référence d'une plateforme : dernière publiée et minimum accepté (en dessous : mise à jour obligatoire).</summary>
public sealed class AppRelease
{
    public const int MaxStoreUrlLength = 300;

    private AppRelease()
    {
    }

    public Guid Id { get; private set; }
    public string AppCode { get; private set; } = default!;
    public string Platform { get; private set; } = default!;
    public string? LatestVersion { get; private set; }
    public string? MinimumSupportedVersion { get; private set; }
    public string StoreUrl { get; private set; } = default!;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    public static AppRelease Create(string appCode, string platform, string storeUrl, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), AppCode = appCode, Platform = platform, StoreUrl = storeUrl, UpdatedAt = now
    };

    public void Update(string? latest, string? minimum, string storeUrl, string by, DateTimeOffset now)
    {
        if (latest is not null && minimum is not null && VersionRules.Compare(minimum, latest) > 0)
        {
            throw new DomainException("La version minimale ne peut pas dépasser la dernière version.");
        }

        if (!Uri.TryCreate(storeUrl.Trim(), UriKind.Absolute, out var url) || url.Scheme != "https" || storeUrl.Length > MaxStoreUrlLength)
        {
            throw new DomainException("Lien du store invalide (https).");
        }

        (LatestVersion, MinimumSupportedVersion, StoreUrl, UpdatedBy, UpdatedAt) = (latest, minimum, storeUrl.Trim(), by, now);
    }

    public UpdateStatus StatusOf(string version) => VersionRules.StatusOf(version, LatestVersion, MinimumSupportedVersion);
}
