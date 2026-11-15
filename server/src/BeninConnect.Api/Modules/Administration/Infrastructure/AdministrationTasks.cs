using System.IO.Compression;
using System.Text;
using BeninConnect.Modules.Administration.Application;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BeninConnect.Modules.Administration.Infrastructure;

/// <summary>
/// Sauvegarde quotidienne de la base, après DailyAtUtc et une fois par jour ; vérifiée régulièrement, donc rattrapée
/// si l'API démarre après l'heure prévue. Puis copie hors du serveur si elle est configurée. Manuelle : faite tout de suite.
/// </summary>
public sealed class DailyBackupTask(
    IBackupStore store, IBackupMirror mirror, BackupSettings settings, BackupScheduleState state, TimeProvider clock, ILogger<DailyBackupTask> logger)
    : IScheduledTask
{
    public bool Enabled => settings.Automatic;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!run.Manual)
        {
            if (TimeOnly.FromDateTime(now.UtcDateTime) < settings.DailyAtUtc)
            {
                return $"Rien à faire avant {settings.DailyAtUtc:HH\\:mm} UTC.";
            }

            if (store.HasDailyBackup(DateOnly.FromDateTime(now.UtcDateTime)))
            {
                return "Sauvegarde du jour déjà faite.";
            }

            // Après un échec, nouvel essai au plus tôt une heure plus tard
            if (state.LastError is not null && state.LastRunAt > now.AddHours(-1))
            {
                return "Nouvel essai dans moins d'une heure (échec précédent).";
            }
        }

        BackupFileDto file;
        try
        {
            file = await store.CreateScheduledAsync(ct);
            state.LastRunAt = clock.GetUtcNow();
            state.LastError = null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            state.LastRunAt = clock.GetUtcNow();
            state.LastError = e.Message;
            throw;
        }

        var summary = $"{file.Name} ({file.Size / 1024} Ko)";
        if (mirror.Enabled)
        {
            try
            {
                var copy = await mirror.SyncAsync(ct);
                summary += $", {copy.Uploaded} fichier(s) copié(s) hors du serveur";
            }
            catch (DomainException e)
            {
                logger.LogWarning("Copie distante après la sauvegarde quotidienne reportée : {Message}", e.Message);
                summary += ", copie distante reportée";
            }
        }

        return summary;
    }
}

/// <summary>Copie des sauvegardes hors du serveur (S3 / R2), si Backups:Offsite est configuré.</summary>
public sealed class BackupMirrorTask(IBackupMirror mirror) : IScheduledTask
{
    public bool Enabled => mirror.Enabled;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var result = await mirror.SyncAsync(ct);
        return $"{result.Uploaded} envoyé(s), {result.Deleted} supprimé(s), {result.Remote} copie(s) distante(s).";
    }
}

/// <summary>
/// Contrôle de la dernière sauvegarde de la base : archive gzip lisible jusqu'au bout, dump terminé par pg_dump,
/// moins de 48 heures, et copie distante de même taille si la copie hors du serveur est configurée.
/// </summary>
public sealed class BackupVerifyTask(BackupSettings settings, IBackupMirror mirror, TimeProvider clock) : IScheduledTask
{
    private static readonly byte[] EndMarker = Encoding.ASCII.GetBytes("dump complete");

    public bool Enabled => true;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var local = Directory.Exists(settings.Directory) ? LocalBackupFiles.List(settings.Directory) : [];
        var latest = local.Where(f => f.Kind is "Daily" or "Manual" && f.Name.EndsWith(".sql.gz", StringComparison.Ordinal))
            .MaxBy(f => f.CreatedAt) ?? throw new DomainException("Aucune sauvegarde de la base sur le serveur.");

        var age = clock.GetUtcNow() - latest.CreatedAt;
        if (age > TimeSpan.FromHours(48))
        {
            throw new DomainException($"Dernière sauvegarde trop ancienne : {latest.Name} ({(int)age.TotalHours} h).");
        }

        await CheckDumpAsync(LocalBackupFiles.PathOf(settings.Directory, latest.Kind, latest.Name), latest.Name, ct);
        var summary = $"{latest.Name} lisible et complète ({latest.Size / 1024} Ko)";

        if (mirror.Enabled)
        {
            var offsite = await mirror.GetStatusAsync([latest], ct);
            if (!offsite.Remote.Any(r => r.Kind == latest.Kind && r.Name == latest.Name && r.Size == latest.Size))
            {
                throw new DomainException($"{latest.Name} : copie distante absente ou incomplète.{(offsite.LastError is null ? "" : $" {offsite.LastError}")}");
            }

            summary += ", copie distante identique";
        }

        return summary + ".";
    }

    /// <summary>Décompresse tout le fichier (contrôle CRC de gzip) et cherche la marque de fin écrite par pg_dump.</summary>
    internal static async Task CheckDumpAsync(string path, string name, CancellationToken ct)
    {
        var tail = new byte[4096];
        var tailLength = 0;
        try
        {
            await using var file = File.OpenRead(path);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            var buffer = new byte[81920];
            int read;
            while ((read = await gzip.ReadAsync(buffer, ct)) > 0)
            {
                // Garde les 4 derniers Ko lus
                if (read >= tail.Length)
                {
                    Array.Copy(buffer, read - tail.Length, tail, 0, tail.Length);
                    tailLength = tail.Length;
                }
                else
                {
                    var keep = Math.Min(tailLength, tail.Length - read);
                    Array.Copy(tail, tailLength - keep, tail, 0, keep);
                    Array.Copy(buffer, 0, tail, keep, read);
                    tailLength = keep + read;
                }
            }
        }
        catch (InvalidDataException)
        {
            throw new DomainException($"{name} : archive gzip corrompue.");
        }

        if (tail.AsSpan(0, tailLength).IndexOf(EndMarker) < 0)
        {
            throw new DomainException($"{name} : sauvegarde incomplète (fin du dump absente).");
        }
    }
}

/// <summary>Journal d'audit : entrées plus anciennes que Audit:RetentionMonths (12 par défaut) supprimées.</summary>
public sealed class AuditRetentionTask(IAdministrationDb db, AuditSettings settings, TimeProvider clock) : IScheduledTask
{
    public bool Enabled => settings.RetentionMonths > 0;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var limit = clock.GetUtcNow().AddMonths(-settings.RetentionMonths);
        var deleted = await db.AuditEntries.Where(e => e.At < limit).ExecuteDeleteAsync(ct);
        return $"{deleted} entrée(s) de plus de {settings.RetentionMonths} mois supprimée(s).";
    }
}

/// <summary>Section « Audit ».</summary>
public sealed class AuditSettings
{
    public const string Section = "Audit";

    /// <summary>0 : conservation illimitée.</summary>
    public int RetentionMonths { get; set; } = 12;
}
