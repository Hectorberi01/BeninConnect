namespace BeninConnect.Modules.AppVersionManagement.Domain;

/// <summary>Une installation de l'application sur un téléphone (identifiant tiré au premier lancement).</summary>
public sealed class AppInstallation
{
    public const int MaxPushTokenLength = 200;

    private AppInstallation()
    {
    }

    public Guid Id { get; private set; }
    public Guid InstallationId { get; private set; }

    /// <summary>Dernier membre connecté sur cette installation.</summary>
    public Guid? UserId { get; private set; }

    public string AppCode { get; private set; } = default!;
    public string Platform { get; private set; } = default!;

    public string Version { get; private set; } = default!;
    public string BuildNumber { get; private set; } = default!;

    public string? OsVersion { get; private set; }
    public string? PushToken { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    public static AppInstallation Create(Guid installationId, string appCode, string platform, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        InstallationId = installationId,
        AppCode = appCode,
        Platform = platform,
        Version = "0",
        BuildNumber = "0",
        FirstSeenAt = now,
        LastSeenAt = now,
    };

    /// <summary>Un appel anonyme ne retire pas le membre (déconnexion : l'installation reste rattachée au dernier compte).</summary>
    public void Report(Guid? userId, string platform, string version, string build, string? osVersion, string? pushToken, DateTimeOffset now)
    {
        if (userId is not null)
        {
            UserId = userId;
        }

        Platform = platform;
        Version = version;
        BuildNumber = build;
        OsVersion = osVersion;
        if (pushToken is not null)
        {
            PushToken = pushToken.Length <= MaxPushTokenLength ? pushToken : null;
        }

        LastSeenAt = now;
    }

    public void Forget() => (UserId, PushToken) = (null, null);
}
