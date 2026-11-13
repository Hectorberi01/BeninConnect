using System.Text.RegularExpressions;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Jobs.Domain;

/// <summary>Configuration d'une source, modifiée dans la console. Le secret est stocké chiffré.</summary>
public sealed partial class JobSourceConfig
{
    public const int MaxSearches = 20;

    private JobSourceConfig()
    {
    }

    public JobSource Source { get; private set; }
    public bool IsEnabled { get; private set; }
    public int ImportIntervalMinutes { get; private set; }

    /// <summary>France Travail : identifiant client ; Adzuna : App ID ; ReliefWeb : appname approuvé.</summary>
    public string? ClientId { get; private set; }

    public string? EncryptedSecret { get; private set; }
    public DateTimeOffset? SecretUpdatedAt { get; private set; }
    public List<JobSearchDefinition> Searches { get; private set; } = [];

    public DateTimeOffset? LastRunAt { get; private set; }
    public DateTimeOffset? LastSuccessAt { get; private set; }
    public string? LastError { get; private set; }
    public int LastAdded { get; private set; }
    public int LastUpdated { get; private set; }
    public int LastDuplicates { get; private set; }
    public int LastHidden { get; private set; }
    public int LastExpired { get; private set; }

    public static bool NeedsSecret(JobSource source) => source is JobSource.FranceTravail or JobSource.Adzuna;

    public static bool NeedsClientId(JobSource source) => source is not JobSource.Remotive;

    /// <summary>Configuration de départ : désactivée, recherches d'exemple.</summary>
    public static JobSourceConfig CreateDefault(JobSource source) => new()
    {
        Source = source,
        ImportIntervalMinutes = JobSources.DefaultIntervalMinutes(source),
        Searches = source switch
        {
            JobSource.FranceTravail =>
            [
                new("dev-idf", Keywords: "développeur", Region: "11"),
                new("logistique-idf", Keywords: "logistique", Region: "11", ContractType: "CDI")
            ],
            JobSource.ReliefWeb => [new("benin", Country: "Benin"), new("togo", Country: "Togo")],
            JobSource.Adzuna => [new("dev-paris", Keywords: "développeur", Location: "Paris")],
            _ => [new("software-dev", Category: "software-dev")]
        }
    };

    public void Configure(bool enabled, int intervalMinutes, string? clientId, IReadOnlyList<JobSearchDefinition> searches)
    {
        var min = JobSources.MinIntervalMinutes(Source);
        if (intervalMinutes < min || intervalMinutes > 7 * 24 * 60)
        {
            throw new DomainException($"Intervalle d'import : {min} minutes au moins, 7 jours au plus.");
        }

        if (enabled && Source == JobSource.Remotive)
        {
            throw new DomainException("Remotive interdit d'afficher ses offres derrière une inscription : impossible tant que BéninConnect exige un compte.");
        }

        var cleaned = searches.Select(Clean).ToList();
        if (cleaned.Count > MaxSearches)
        {
            throw new DomainException($"{MaxSearches} recherches au plus.");
        }

        if (cleaned.GroupBy(s => s.Key).Any(g => g.Count() > 1))
        {
            throw new DomainException("Chaque recherche doit avoir une clé différente.");
        }

        if (Source == JobSource.ReliefWeb && cleaned.Any(s => s.Country is null))
        {
            throw new DomainException("ReliefWeb : indiquez le pays de chaque recherche (en anglais, ex. Benin).");
        }

        var id = string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim();
        if (enabled && NeedsClientId(Source) && id is null)
        {
            throw new DomainException(Source == JobSource.ReliefWeb ? "ReliefWeb : l'appname approuvé est obligatoire." : "Identifiant de l'application obligatoire.");
        }

        if (enabled && cleaned.Count == 0)
        {
            throw new DomainException("Ajoutez au moins une recherche.");
        }

        IsEnabled = enabled;
        ImportIntervalMinutes = intervalMinutes;
        ClientId = id is null ? null : JobText.Cut(id, 200);
        Searches = cleaned;
    }

    public void SetSecret(string? encrypted, DateTimeOffset now)
    {
        EncryptedSecret = encrypted;
        SecretUpdatedAt = encrypted is null ? null : now;
    }

    public bool IsDue(DateTimeOffset now) => IsEnabled && (LastRunAt is null || LastRunAt.Value.AddMinutes(ImportIntervalMinutes) <= now);

    public void Started(DateTimeOffset now) => LastRunAt = now;

    /// <summary>Import terminé. Erreur partielle (une recherche en échec) : gardée, l'import compte quand même.</summary>
    public void Completed(int added, int updated, int duplicates, int hidden, int expired, string? error, DateTimeOffset now)
    {
        LastSuccessAt = now;
        LastError = error is null ? null : JobText.Cut(error, 1000);
        (LastAdded, LastUpdated, LastDuplicates, LastHidden, LastExpired) = (added, updated, duplicates, hidden, expired);
    }

    public void Failed(string error) => LastError = JobText.Cut(error, 1000);

    private static JobSearchDefinition Clean(JobSearchDefinition s)
    {
        var key = (s.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (!KeyPattern().IsMatch(key))
        {
            throw new DomainException($"Clé de recherche « {s.Key} » : 2 à 40 caractères, lettres minuscules, chiffres et tirets.");
        }

        return new JobSearchDefinition(key, Opt(s.Keywords, 200), Opt(s.Region, 10), Opt(s.Department, 200), Opt(s.ContractType, 50),
            Opt(s.Country, 100), Opt(s.Category, 100), Opt(s.Location, 100));
    }

    private static string? Opt(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : JobText.Cut(value, max);

    [GeneratedRegex("^[a-z0-9-]{2,40}$")]
    private static partial Regex KeyPattern();
}

/// <summary>Réglages communs (ligne unique) : mots-clés d'alerte et seuil de signalements.</summary>
public sealed class JobSettings
{
    public const int SingletonId = 1;

    private JobSettings()
    {
    }

    public int Id { get; private set; }
    public List<string> AlertKeywords { get; private set; } = [];
    public int ReportThreshold { get; private set; }

    public static JobSettings CreateDefault() => new()
    {
        Id = SingletonId,
        ReportThreshold = 3,
        AlertKeywords =
        [
            "frais de dossier", "frais d'inscription", "visa garanti", "western union", "moneygram", "paiement avant embauche",
            "envoyez de l'argent", "mandat cash"
        ]
    };

    public void Update(IEnumerable<string> keywords, int threshold)
    {
        if (threshold is < 1 or > 50)
        {
            throw new DomainException("Seuil de signalements : entre 1 et 50.");
        }

        var list = keywords.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count > 200 || list.Any(k => k.Length > 100))
        {
            throw new DomainException("200 mots-clés au plus, 100 caractères chacun.");
        }

        AlertKeywords = list;
        ReportThreshold = threshold;
    }
}
