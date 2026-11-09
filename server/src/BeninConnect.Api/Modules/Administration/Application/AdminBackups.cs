using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Administration.Application;

/// <summary>Kind : Manual, Daily, Weekly, Monthly, Media (photos). Offsite : copie présente dans le stockage distant.</summary>
public sealed record BackupFileDto(string Kind, string Name, long Size, DateTimeOffset CreatedAt, bool CanDelete, bool Offsite = false);

/// <summary>Copie distante (S3) : Kind et Name déduits de la clé « préfixe/dossier/nom ».</summary>
public sealed record RemoteBackupDto(string Kind, string Name, long Size, DateTimeOffset UploadedAt, bool IsLocal);

/// <summary>Copie hors du serveur : bucket S3/R2, dernière synchronisation, contenu distant.</summary>
public sealed record OffsiteStatusDto(
    bool Enabled, string? Bucket, int RetentionDays, int IntervalMinutes, DateTimeOffset? LastSyncAt, string? LastError,
    long RemoteBytes, IReadOnlyList<RemoteBackupDto> Remote);

/// <summary>État des sauvegardes : dossier monté, pg_dump présent, dernière sauvegarde, espace libre, copie distante.</summary>
public sealed record BackupOverviewDto(
    bool DirectoryAvailable, bool CanCreate, string? PgDumpVersion, DateTimeOffset? LastDatabaseBackupAt, long TotalBytes, long? FreeBytes,
    IReadOnlyList<BackupFileDto> Files, OffsiteStatusDto? Offsite = null);

public sealed record MirrorResultDto(int Uploaded, int Deleted, int Remote, DateTimeOffset At);

/// <summary>Objet du stockage distant.</summary>
public sealed record RemoteObject(string Key, long Size, DateTimeOffset LastModified);

/// <summary>Stockage distant des sauvegardes (S3, R2…). Clés relatives au préfixe configuré.</summary>
public interface IOffsiteBackupStore
{
    Task<IReadOnlyList<RemoteObject>> ListAsync(CancellationToken ct);

    Task UploadAsync(string key, string path, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);

    Task<Stream?> OpenAsync(string key, CancellationToken ct);
}

/// <summary>Copie des sauvegardes vers le stockage distant (manuelle ou périodique).</summary>
public interface IBackupMirror
{
    bool Enabled { get; }

    Task<OffsiteStatusDto> GetStatusAsync(IReadOnlyCollection<BackupFileDto> local, CancellationToken ct);

    Task<MirrorResultDto> SyncAsync(CancellationToken ct);

    Task<Stream?> OpenAsync(string kind, string name, CancellationToken ct);
}

public sealed record BackupDownload(Stream Content, string FileName, string ContentType);

public interface IBackupStore
{
    Task<BackupOverviewDto> GetOverviewAsync(CancellationToken ct);

    /// <summary>Sauvegarde immédiate de la base (pg_dump) dans manual/.</summary>
    Task<BackupFileDto> CreateAsync(CancellationToken ct);

    BackupDownload? Open(string kind, string name);

    /// <summary>Seules les sauvegardes manuelles sont supprimables (la rotation automatique gère les autres).</summary>
    bool DeleteManual(string name);
}

public sealed record GetBackupsQuery : IQuery<BackupOverviewDto>;

public sealed record CreateBackupCommand : ICommand<BackupFileDto>;

public sealed record DownloadBackupQuery(string Kind, string Name) : IQuery<BackupDownload>;

public sealed record DeleteBackupCommand(string Name) : ICommand<bool>;

public sealed record SyncOffsiteCommand : ICommand<MirrorResultDto>;

public sealed record DownloadOffsiteQuery(string Kind, string Name) : IQuery<BackupDownload>;

public sealed class BackupHandlers(IBackupStore store, IBackupMirror mirror, AdminSession admin) :
    IRequestHandler<GetBackupsQuery, BackupOverviewDto>,
    IRequestHandler<CreateBackupCommand, BackupFileDto>,
    IRequestHandler<DownloadBackupQuery, BackupDownload>,
    IRequestHandler<DeleteBackupCommand, bool>,
    IRequestHandler<SyncOffsiteCommand, MirrorResultDto>,
    IRequestHandler<DownloadOffsiteQuery, BackupDownload>
{
    public async Task<BackupOverviewDto> Handle(GetBackupsQuery request, CancellationToken ct)
    {
        var overview = await store.GetOverviewAsync(ct);
        if (!mirror.Enabled)
        {
            return overview;
        }

        var offsite = await mirror.GetStatusAsync(overview.Files, ct);
        var copied = offsite.Remote.Select(r => (r.Kind, r.Name)).ToHashSet();
        return overview with
        {
            Files = overview.Files.Select(f => f with { Offsite = copied.Contains((f.Kind, f.Name)) }).ToList(),
            Offsite = offsite
        };
    }

    public async Task<MirrorResultDto> Handle(SyncOffsiteCommand request, CancellationToken ct)
    {
        if (!mirror.Enabled)
        {
            throw new DomainException("Copie distante non configurée (Backups:Offsite:Bucket).");
        }

        var result = await mirror.SyncAsync(ct);
        await admin.LogAsync("Sauvegardes copiées hors du serveur", "Backup", null, $"{result.Uploaded} envoyée(s), {result.Deleted} supprimée(s)", ct);
        return result;
    }

    public async Task<BackupDownload> Handle(DownloadOffsiteQuery request, CancellationToken ct)
    {
        BackupNames.EnsureValid(request.Kind, request.Name);
        var stream = await mirror.OpenAsync(request.Kind, request.Name, ct) ?? throw new NotFoundException("Copie distante");
        await admin.LogAsync("Copie distante téléchargée", "Backup", null, $"{request.Kind}/{request.Name}", ct);
        return new BackupDownload(stream, request.Name, "application/gzip");
    }

    public async Task<BackupFileDto> Handle(CreateBackupCommand request, CancellationToken ct)
    {
        var file = await store.CreateAsync(ct);
        await admin.LogAsync("Sauvegarde créée", "Backup", null, $"{file.Name} ({file.Size} octets)", ct);
        if (mirror.Enabled)
        {
            // Copie distante aussitôt, sans faire attendre la console (erreur visible dans l'état de la copie)
            _ = Task.Run(async () =>
            {
                try
                {
                    await mirror.SyncAsync(CancellationToken.None);
                }
                catch (DomainException)
                {
                    // déjà journalisé par BackupMirror
                }
            }, CancellationToken.None);
        }

        return file;
    }

    public async Task<BackupDownload> Handle(DownloadBackupQuery request, CancellationToken ct)
    {
        var file = store.Open(request.Kind, request.Name) ?? throw new NotFoundException("Sauvegarde");
        await admin.LogAsync("Sauvegarde téléchargée", "Backup", null, $"{request.Kind}/{request.Name}", ct);
        return file;
    }

    public async Task<bool> Handle(DeleteBackupCommand request, CancellationToken ct)
    {
        if (!store.DeleteManual(request.Name))
        {
            throw new NotFoundException("Sauvegarde");
        }

        await admin.LogAsync("Sauvegarde supprimée", "Backup", null, request.Name, ct);
        return true;
    }
}

public sealed class BackupSettings
{
    public const string Section = "Backups";

    /// <summary>Dossier partagé avec le conteneur de sauvegarde (/backups en production).</summary>
    public string Directory { get; set; } = "/backups";

    public string PgDump { get; set; } = "pg_dump";

    public int TimeoutMinutes { get; set; } = 15;

    public OffsiteSettings Offsite { get; set; } = new();
}

/// <summary>Copie des sauvegardes dans un bucket S3 compatible (Cloudflare R2…). Désactivée sans Bucket.</summary>
public sealed class OffsiteSettings
{
    public string Bucket { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = string.Empty;
    public string Region { get; set; } = "auto";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Prefix { get; set; } = "backups/";

    /// <summary>Copies distantes absentes du serveur supprimées au-delà de ce délai.</summary>
    public int RetentionDays { get; set; } = 90;

    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Copier aussi les archives de photos (backups/media).</summary>
    public bool IncludeMedia { get; set; } = true;

    public bool Enabled => !string.IsNullOrWhiteSpace(Bucket);
}

/// <summary>Noms acceptés : pas de chemin, extensions de sauvegarde uniquement.</summary>
public static class BackupNames
{
    public static readonly IReadOnlyDictionary<string, string> Folders = new Dictionary<string, string>
    {
        ["Manual"] = "manual", ["Daily"] = "daily", ["Weekly"] = "weekly", ["Monthly"] = "monthly", ["Media"] = "media"
    };

    public static bool IsValid(string name) =>
        name.Length is > 0 and <= 120 && !name.Contains("..") && !name.StartsWith('.')
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
        && (name.EndsWith(".sql.gz", StringComparison.Ordinal) || name.EndsWith(".tar.gz", StringComparison.Ordinal));

    public static void EnsureValid(string kind, string name)
    {
        if (!Folders.ContainsKey(kind) || !IsValid(name))
        {
            throw new DomainException("Nom de sauvegarde invalide.");
        }
    }
}
