using System.Diagnostics;
using BeninConnect.Modules.Administration.Application;
using BeninConnect.Shared.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BeninConnect.Modules.Administration.Infrastructure;

/// <summary>
/// Sauvegardes sur disque : celles du conteneur « backup » (daily, weekly, monthly), les photos (media)
/// et les sauvegardes manuelles faites ici avec pg_dump (manual).
/// </summary>
public sealed class BackupStore(BackupSettings settings, IConfiguration configuration, TimeProvider clock, ILogger<BackupStore> logger) : IBackupStore
{
    private static readonly SemaphoreSlim Running = new(1, 1);
    private static string? _pgDumpVersion;
    private static bool _pgDumpChecked;

    private string Root => settings.Directory;

    public async Task<BackupOverviewDto> GetOverviewAsync(CancellationToken ct)
    {
        var available = Directory.Exists(Root);
        var files = available ? LocalBackupFiles.List(Root) : [];
        var version = await PgDumpVersionAsync(ct);
        long? free = null;
        if (available)
        {
            try
            {
                free = new DriveInfo(Path.GetFullPath(Root)).AvailableFreeSpace;
            }
            catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
            {
                free = null;
            }
        }

        return new BackupOverviewDto(
            available, available && version is not null, version,
            files.Where(f => f.Kind != "Media").Select(f => (DateTimeOffset?)f.CreatedAt).DefaultIfEmpty(null).Max(),
            files.Sum(f => f.Size), free, files);
    }

    public async Task<BackupFileDto> CreateAsync(CancellationToken ct)
    {
        var db = Database;
        var info = await DumpAsync("manual", $"{db.Database}-{clock.GetUtcNow():yyyyMMdd-HHmmss}.sql.gz", wait: false, ct);
        return new BackupFileDto("Manual", info.Name, info.Length, info.LastWriteTimeUtc, true);
    }

    public bool HasDailyBackup(DateOnly day) =>
        File.Exists(Path.Combine(Root, "daily", $"{Database.Database}-{day:yyyyMMdd}.sql.gz"));

    public async Task<BackupFileDto> CreateScheduledAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var db = Database.Database;
        var daily = await DumpAsync("daily", $"{db}-{now:yyyyMMdd}.sql.gz", wait: true, ct);

        // Semaine (ISO) et mois en cours : copie de la dernière sauvegarde du jour, remplacée jusqu'à la fin de la période
        var week = $"{db}-{System.Globalization.ISOWeek.GetYear(now.UtcDateTime)}{System.Globalization.ISOWeek.GetWeekOfYear(now.UtcDateTime):00}.sql.gz";
        CopyInto("weekly", week, daily.FullName);
        CopyInto("monthly", $"{db}-{now:yyyyMM}.sql.gz", daily.FullName);

        Rotate("daily", now.AddDays(-settings.KeepDays));
        Rotate("weekly", now.AddDays(-7 * settings.KeepWeeks - 1));
        Rotate("monthly", now.AddMonths(-settings.KeepMonths).AddDays(-1));
        return new BackupFileDto("Daily", daily.Name, daily.Length, daily.LastWriteTimeUtc, false);
    }

    private NpgsqlConnectionStringBuilder Database => new(configuration.GetConnectionString("Database"));

    private void CopyInto(string folder, string name, string source)
    {
        var dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        File.Copy(source, Path.Combine(dir, name), overwrite: true);
    }

    /// <summary>Supprime les sauvegardes plus anciennes que la limite (date du fichier).</summary>
    private void Rotate(string folder, DateTimeOffset limit)
    {
        var dir = Path.Combine(Root, folder);
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (var file in new DirectoryInfo(dir).EnumerateFiles("*.sql.gz").Where(f => f.LastWriteTimeUtc < limit.UtcDateTime))
        {
            file.Delete();
            logger.LogInformation("Sauvegarde expirée supprimée : {Folder}/{Name}", folder, file.Name);
        }
    }

    /// <summary>pg_dump vers dossier/nom (fichier .partial puis renommé). wait : attendre une sauvegarde en cours plutôt que refuser.</summary>
    private async Task<FileInfo> DumpAsync(string folderName, string name, bool wait, CancellationToken ct)
    {
        if (!Directory.Exists(Root))
        {
            throw new DomainException("Dossier des sauvegardes introuvable sur le serveur.");
        }

        if (await PgDumpVersionAsync(ct) is null)
        {
            throw new DomainException("pg_dump n'est pas installé dans l'API : sauvegarde impossible.");
        }

        if (wait)
        {
            await Running.WaitAsync(ct);
        }
        else if (!await Running.WaitAsync(0, ct))
        {
            throw new DomainException("Une sauvegarde est déjà en cours.");
        }

        try
        {
            var db = Database;
            var folder = Path.Combine(Root, folderName);
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, name);
            var partial = target + ".partial";

            var start = new ProcessStartInfo(settings.PgDump)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                ArgumentList = { "--no-owner", "--no-privileges", "-Z", "6", "-f", partial }
            };
            // Connexion par variables d'environnement : le mot de passe n'apparaît pas dans la ligne de commande.
            start.Environment["PGHOST"] = db.Host;
            start.Environment["PGPORT"] = db.Port.ToString();
            start.Environment["PGDATABASE"] = db.Database;
            start.Environment["PGUSER"] = db.Username;
            start.Environment["PGPASSWORD"] = db.Password;
            start.Environment["PGSSLMODE"] = db.SslMode.ToString().ToLowerInvariant() switch
            {
                "verifyca" => "verify-ca",
                "verifyfull" => "verify-full",
                var mode => mode
            };

            using var process = Process.Start(start) ?? throw new DomainException("Impossible de lancer pg_dump.");
            var errors = process.StandardError.ReadToEndAsync(ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(settings.TimeoutMinutes));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                File.Delete(partial);
                throw new DomainException("Sauvegarde interrompue : délai dépassé.");
            }

            if (process.ExitCode != 0)
            {
                File.Delete(partial);
                var message = (await errors).Trim();
                logger.LogError("pg_dump a échoué ({Code}) : {Message}", process.ExitCode, message);
                var first = message.Split('\n')[0];
                throw new DomainException(first.Contains("server version mismatch", StringComparison.OrdinalIgnoreCase)
                    ? $"Version de PostgreSQL plus récente que pg_dump : {first}"
                    : $"La sauvegarde a échoué : {first}");
            }

            File.Move(partial, target, overwrite: true);
            return new FileInfo(target);
        }
        finally
        {
            Running.Release();
        }
    }

    public BackupDownload? Open(string kind, string name)
    {
        BackupNames.EnsureValid(kind, name);
        var path = Path.Combine(Root, BackupNames.Folders[kind], name);
        if (!File.Exists(path))
        {
            return null;
        }

        return new BackupDownload(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true),
            name, "application/gzip");
    }

    public bool DeleteManual(string name)
    {
        BackupNames.EnsureValid("Manual", name);
        var path = Path.Combine(Root, "manual", name);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private async Task<string?> PgDumpVersionAsync(CancellationToken ct)
    {
        if (_pgDumpChecked)
        {
            return _pgDumpVersion;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(settings.PgDump, "--version")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
            });
            if (process is not null)
            {
                var output = await process.StandardOutput.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);
                _pgDumpVersion = process.ExitCode == 0 ? output.Trim() : null;
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            _pgDumpVersion = null;
        }

        _pgDumpChecked = true;
        return _pgDumpVersion;
    }
}

/// <summary>Sauvegardes présentes sur le disque (dossiers manual, daily, weekly, monthly, media).</summary>
public static class LocalBackupFiles
{
    public static List<BackupFileDto> List(string root)
    {
        var result = new List<BackupFileDto>();
        foreach (var (kind, folder) in BackupNames.Folders)
        {
            var dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var file in new DirectoryInfo(dir).EnumerateFiles())
            {
                // Liens « -latest » du conteneur de sauvegarde : doublons
                if (!BackupNames.IsValid(file.Name) || file.Name.Contains("-latest", StringComparison.Ordinal) || file.LinkTarget is not null)
                {
                    continue;
                }

                result.Add(new BackupFileDto(kind, file.Name, file.Length, file.LastWriteTimeUtc, kind == "Manual"));
            }
        }

        return result.OrderByDescending(f => f.CreatedAt).ToList();
    }

    public static string PathOf(string root, string kind, string name) => Path.Combine(root, BackupNames.Folders[kind], name);
}
