namespace BeninConnect.App.Core.Services;

/// <summary>Configuration de démarrage.</summary>
public sealed record AppOptions
{
    /// <summary>true : repositories mockés en mémoire. false : API ASP.NET Core (server/), voir docs/REMPLACEMENT_API.md.</summary>
    public bool UseMockData { get; init; } = true;

    /// <summary>URL de l'API ASP.NET Core (utilisée quand UseMockData = false).</summary>
    public Uri? ApiBaseUrl { get; init; }

    /// <summary>Latence simulée de chaque appel de repository mocké.</summary>
    public TimeSpan SimulatedLatency { get; init; } = TimeSpan.FromMilliseconds(180);

    /// <summary>Durée d'affichage du splash screen.</summary>
    public TimeSpan SplashDuration { get; init; } = TimeSpan.FromMilliseconds(1200);

    /// <summary>Fichier des préférences locales. null = préférences en mémoire (tests).</summary>
    public string? PreferencesFilePath { get; init; } = DefaultPreferencesPath();

    /// <summary>Active les options développeur (erreurs simulées).</summary>
    public bool IsDevelopment { get; init; } = true;

    /// <summary>Applique le thème à Application.Current (désactivé dans les tests unitaires).</summary>
    public bool ApplyThemeToApplication { get; init; } = true;

    public static string DefaultPreferencesPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BeninConnect",
            "preferences.json");

    /// <summary>Délai maximal d'un appel HTTP.</summary>
    public TimeSpan ApiTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public const string DefaultApiUrl = "http://localhost:5080";

    /// <summary>
    /// Source de données choisie dans Paramètres › Mode développeur (appliquée au démarrage).
    /// La variable d'environnement BENINCONNECT_API_URL force l'API (pratique sur desktop).
    /// </summary>
    public AppOptions WithDataSourceFrom(IPreferencesService preferences)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("BENINCONNECT_API_URL");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && Uri.TryCreate(fromEnvironment, UriKind.Absolute, out var envUrl))
        {
            return this with { UseMockData = false, ApiBaseUrl = envUrl };
        }

        if (preferences.Get(PreferenceKeys.DataSource) == "api"
            && Uri.TryCreate(preferences.Get(PreferenceKeys.ApiUrl) ?? DefaultApiUrl, UriKind.Absolute, out var url))
        {
            return this with { UseMockData = false, ApiBaseUrl = url };
        }

        return this;
    }
}
