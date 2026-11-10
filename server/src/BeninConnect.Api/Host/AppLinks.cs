namespace BeninConnect.Host;

/// <summary>
/// Liens universels iOS et App Links Android : https://monbenin.com/post/… ouvre directement l'application installée.
/// Fichiers servis sous /.well-known (relayés par Caddy depuis monbenin.com). Absents (404) tant que la configuration est vide.
/// </summary>
public sealed class AppLinkSettings
{
    public const string Section = "AppLinks";

    /// <summary>Team ID Apple (10 caractères, developer.apple.com > Membership).</summary>
    public string AppleTeamId { get; set; } = string.Empty;
    public string IosBundleId { get; set; } = "com.hbatech.beninconnect.rn";
    public string AndroidPackage { get; set; } = "com.hbatech.beninconnect.rn";

    /// <summary>Empreintes SHA-256 du certificat de signature (Play Console > Intégrité de l'application), séparées par des virgules.</summary>
    public string AndroidCertSha256 { get; set; } = string.Empty;
}

public static class AppLinks
{
    /// <summary>Chemins ouverts dans l'application (le reste du site reste dans le navigateur).</summary>
    private static readonly string[] Paths = ["/post/*"];

    public static void MapAppLinks(this WebApplication app)
    {
        var settings = app.Configuration.GetSection(AppLinkSettings.Section).Get<AppLinkSettings>() ?? new AppLinkSettings();

        app.MapGet("/.well-known/apple-app-site-association", () => string.IsNullOrWhiteSpace(settings.AppleTeamId)
                ? Results.NotFound()
                : Results.Json(new
                {
                    applinks = new
                    {
                        details = new[]
                        {
                            new { appIDs = new[] { $"{settings.AppleTeamId.Trim()}.{settings.IosBundleId}" }, components = Paths.Select(p => new Dictionary<string, string> { ["/"] = p }) }
                        }
                    }
                }))
            .AllowAnonymous().ExcludeFromDescription();

        app.MapGet("/.well-known/assetlinks.json", () =>
            {
                var prints = settings.AndroidCertSha256.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return prints.Length == 0
                    ? Results.NotFound()
                    : Results.Json(new[]
                    {
                        new
                        {
                            relation = new[] { "delegate_permission/common.handle_all_urls" },
                            target = new { @namespace = "android_app", package_name = settings.AndroidPackage, sha256_cert_fingerprints = prints }
                        }
                    });
            })
            .AllowAnonymous().ExcludeFromDescription();
    }
}
