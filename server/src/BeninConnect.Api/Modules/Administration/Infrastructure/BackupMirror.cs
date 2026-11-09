using BeninConnect.Modules.Administration.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BeninConnect.Modules.Administration.Infrastructure;

/// <summary>
/// Copie hors du serveur : envoie les sauvegardes absentes ou modifiées (même nom, autre taille : sauvegarde de la semaine
/// ou du mois remplacée) et supprime les copies distantes disparues du serveur depuis plus de RetentionDays.
/// </summary>
public sealed class BackupMirror(BackupSettings settings, IOffsiteBackupStore remote, TimeProvider clock, ILogger<BackupMirror> logger) : IBackupMirror
{
    private readonly SemaphoreSlim _running = new(1, 1);
    private OffsiteSettings Offsite => settings.Offsite;
    private DateTimeOffset? _lastSyncAt;
    private string? _lastError;

    public bool Enabled => Offsite.Enabled;

    public async Task<OffsiteStatusDto> GetStatusAsync(IReadOnlyCollection<BackupFileDto> local, CancellationToken ct)
    {
        var remoteFiles = new List<RemoteBackupDto>();
        var error = _lastError;
        try
        {
            var names = local.Select(f => (f.Kind, f.Name)).ToHashSet();
            foreach (var o in await remote.ListAsync(ct))
            {
                if (Parse(o.Key) is { } id)
                {
                    remoteFiles.Add(new RemoteBackupDto(id.Kind, id.Name, o.Size, o.LastModified, names.Contains(id)));
                }
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            error = $"Stockage distant injoignable : {e.Message}";
        }

        return new OffsiteStatusDto(true, Offsite.Bucket, Offsite.RetentionDays, Offsite.IntervalMinutes, _lastSyncAt, error,
            remoteFiles.Sum(r => r.Size), remoteFiles.OrderByDescending(r => r.UploadedAt).ToList());
    }

    public async Task<MirrorResultDto> SyncAsync(CancellationToken ct)
    {
        await _running.WaitAsync(ct);
        try
        {
            var local = Directory.Exists(settings.Directory) ? LocalBackupFiles.List(settings.Directory) : [];
            if (!Offsite.IncludeMedia)
            {
                local = local.Where(f => f.Kind != "Media").ToList();
            }

            var existing = (await remote.ListAsync(ct)).ToDictionary(o => o.Key);
            var uploaded = 0;
            foreach (var file in local)
            {
                var key = KeyOf(file.Kind, file.Name);
                if (existing.TryGetValue(key, out var copy) && copy.Size == file.Size)
                {
                    continue;
                }

                await remote.UploadAsync(key, LocalBackupFiles.PathOf(settings.Directory, file.Kind, file.Name), ct);
                uploaded++;
            }

            // Rétention : seulement les copies qui n'existent plus sur le serveur
            var localKeys = local.Select(f => KeyOf(f.Kind, f.Name)).ToHashSet();
            var limit = clock.GetUtcNow().AddDays(-Offsite.RetentionDays);
            var deleted = 0;
            foreach (var o in existing.Values.Where(o => !localKeys.Contains(o.Key) && o.LastModified < limit && Parse(o.Key) is not null))
            {
                await remote.DeleteAsync(o.Key, ct);
                deleted++;
            }

            _lastSyncAt = clock.GetUtcNow();
            _lastError = null;
            logger.LogInformation("Sauvegardes copiées hors du serveur : {Uploaded} envoyée(s), {Deleted} supprimée(s)", uploaded, deleted);
            return new MirrorResultDto(uploaded, deleted, existing.Count + uploaded - deleted, _lastSyncAt.Value);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _lastError = e.Message;
            logger.LogError(e, "Copie des sauvegardes hors du serveur impossible");
            throw new Shared.Domain.DomainException($"Copie distante impossible : {e.Message}");
        }
        finally
        {
            _running.Release();
        }
    }

    public Task<Stream?> OpenAsync(string kind, string name, CancellationToken ct) => remote.OpenAsync(KeyOf(kind, name), ct);

    private static string KeyOf(string kind, string name) => $"{BackupNames.Folders[kind]}/{name}";

    /// <summary>« daily/nom.sql.gz » → (Daily, nom.sql.gz) ; null pour toute autre clé.</summary>
    public static (string Kind, string Name)? Parse(string key)
    {
        var slash = key.IndexOf('/');
        if (slash <= 0)
        {
            return null;
        }

        var folder = key[..slash];
        var name = key[(slash + 1)..];
        var kind = BackupNames.Folders.FirstOrDefault(f => f.Value == folder).Key;
        return kind is not null && BackupNames.IsValid(name) ? (kind, name) : null;
    }
}

/// <summary>Synchronisation périodique (première après 5 minutes, puis toutes les IntervalMinutes).</summary>
public sealed class BackupMirrorWorker(IServiceProvider services, BackupSettings settings, ILogger<BackupMirrorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Offsite.Enabled)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(5, settings.Offsite.IntervalMinutes)));
            do
            {
                try
                {
                    await services.GetRequiredService<IBackupMirror>().SyncAsync(stoppingToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning("Synchronisation des sauvegardes reportée : {Message}", e.Message);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // arrêt de l'application
        }
    }
}

/// <summary>Copie distante non configurée.</summary>
public sealed class DisabledOffsiteStore : IOffsiteBackupStore
{
    public Task<IReadOnlyList<RemoteObject>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RemoteObject>>([]);

    public Task UploadAsync(string key, string path, CancellationToken ct) => throw new InvalidOperationException("Copie distante désactivée.");

    public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;

    public Task<Stream?> OpenAsync(string key, CancellationToken ct) => Task.FromResult<Stream?>(null);
}
