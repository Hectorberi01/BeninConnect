namespace BeninConnect.App.Core.Services;

/// <summary>Réglages de démonstration modifiables à chaud (écran Paramètres > Développeur).</summary>
public interface IDeveloperOptions
{
    bool IsAvailable { get; }

    /// <summary>Fait échouer aléatoirement les appels de données pour tester les états d'erreur.</summary>
    bool SimulateErrors { get; set; }

    TimeSpan Latency { get; set; }
}

public sealed class DeveloperOptions(AppOptions options) : IDeveloperOptions
{
    public bool IsAvailable { get; } = options.IsDevelopment;

    public bool SimulateErrors { get; set; }

    public double FailureRate { get; set; } = 0.5;

    public TimeSpan Latency { get; set; } = options.SimulatedLatency;
}
