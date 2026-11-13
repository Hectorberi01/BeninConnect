using BeninConnect.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BeninConnect.Modules.Jobs.Application;

public interface IJobsDb
{
    DbSet<JobOffer> Offers { get; }

    DbSet<JobOfferReport> Reports { get; }

    DbSet<JobOfferFavorite> Favorites { get; }

    DbSet<JobSourceConfig> Sources { get; }

    DbSet<JobSettings> Settings { get; }

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Identifiants de la source pour un import (jamais de donnée personnelle de membre).</summary>
public sealed record JobSourceContext(string? ClientId, string? Secret);

/// <summary>Adaptateur d'une API d'offres d'emploi.</summary>
public interface IJobOfferSource
{
    JobSource Source { get; }

    IAsyncEnumerable<ImportedOfferData> FetchRecentAsync(JobSourceContext context, JobSearchDefinition search, int sinceDays, CancellationToken ct);

    /// <summary>Faux : offre retirée chez la source. Null : la source ne permet pas de le savoir.</summary>
    Task<bool?> StillExistsAsync(JobSourceContext context, string externalId, CancellationToken ct);
}

/// <summary>Chiffrement des secrets saisis dans la console.</summary>
public interface ISecretProtector
{
    string Protect(string secret);

    /// <summary>Null si illisible (clé de chiffrement changée).</summary>
    string? Unprotect(string protectedSecret);
}

/// <summary>Section « Jobs » : réglages techniques. Sources, recherches et secrets se règlent dans la console.</summary>
public sealed class JobsOptions
{
    public const string Section = "Jobs";

    /// <summary>Imports automatiques (faux en revue Apple / Google et dans les tests).</summary>
    public bool WorkerEnabled { get; set; } = true;

    public int FirstImportDays { get; set; } = 31;
    public int NextImportDays { get; set; } = 3;
    public int RecheckAfterDays { get; set; } = 3;
    public int RechecksPerRun { get; set; } = 50;

    /// <summary>Source qui ne permet pas de vérifier une offre : expirée si absente des imports depuis ce délai.</summary>
    public int UnverifiableExpireAfterDays { get; set; } = 30;

    public int PurgeAfterDays { get; set; } = 90;
    public int RequestDelayMs { get; set; } = 300;
    public int MaxRetries { get; set; } = 3;

    /// <summary>Secrets passés par l'environnement : prioritaires sur ceux de la console.</summary>
    public SecretOverride FranceTravail { get; set; } = new();

    public SecretOverride Adzuna { get; set; } = new();

    public string? EnvironmentSecret(JobSource source) => source switch
    {
        JobSource.FranceTravail => FranceTravail.ClientSecret,
        JobSource.Adzuna => Adzuna.AppKey,
        _ => null
    } is { Length: > 0 } s ? s : null;
}

public sealed class SecretOverride
{
    public string? ClientSecret { get; set; }
    public string? AppKey { get; set; }
}

public sealed record ImportReport(JobSource Source, int Fetched, int Added, int Updated, int Duplicates, int Hidden, int Expired, IReadOnlyList<string> Errors);
