using BeninConnect.Modules.Administration.Application;
using BeninConnect.Modules.Administration.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace BeninConnect.Api.Tests;

/// <summary>Copie hors du serveur : envois, remplacements, rétention (stockage distant simulé en mémoire).</summary>
public sealed class BackupMirrorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"bc-mirror-{Guid.NewGuid():N}");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class MemoryStore : IOffsiteBackupStore
    {
        public Dictionary<string, RemoteObject> Objects { get; } = [];
        public List<string> Uploads { get; } = [];
        public DateTimeOffset Now { get; set; }

        public Task<IReadOnlyList<RemoteObject>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RemoteObject>>(Objects.Values.ToList());

        public Task UploadAsync(string key, string path, CancellationToken ct)
        {
            Uploads.Add(key);
            Objects[key] = new RemoteObject(key, new FileInfo(path).Length, Now);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            Objects.Remove(key);
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenAsync(string key, CancellationToken ct) =>
            Task.FromResult<Stream?>(Objects.ContainsKey(key) ? new MemoryStream([1, 2, 3]) : null);
    }

    private void Write(string folder, string name, int size)
    {
        Directory.CreateDirectory(Path.Combine(_root, folder));
        File.WriteAllBytes(Path.Combine(_root, folder, name), new byte[size]);
    }

    [Fact]
    public async Task Sync_uploads_new_and_changed_files_and_applies_retention()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));
        var store = new MemoryStore { Now = clock.GetUtcNow() };
        var settings = new BackupSettings
        {
            Directory = _root,
            Offsite = new OffsiteSettings { Bucket = "beninconnect-backups", RetentionDays = 30, IncludeMedia = false },
        };
        var mirror = new BackupMirror(settings, store, clock, NullLogger<BackupMirror>.Instance);

        Write("daily", "beninconnect-20261109.sql.gz", 100);
        Write("weekly", "beninconnect-202645.sql.gz", 200);
        Write("manual", "beninconnect-20261109-101500.sql.gz", 50);
        Write("media", "photos-2026-11-09.tar.gz", 70);
        // Copies distantes : une ancienne disparue du serveur (supprimée), une récente disparue (gardée), un fichier étranger (ignoré)
        store.Objects["daily/beninconnect-20260901.sql.gz"] = new("daily/beninconnect-20260901.sql.gz", 10, clock.GetUtcNow().AddDays(-60));
        store.Objects["daily/beninconnect-20261101.sql.gz"] = new("daily/beninconnect-20261101.sql.gz", 10, clock.GetUtcNow().AddDays(-8));
        store.Objects["notes/readme.txt"] = new("notes/readme.txt", 5, clock.GetUtcNow().AddDays(-400));

        var first = await mirror.SyncAsync(CancellationToken.None);
        Assert.Equal(3, first.Uploaded); // photos exclues
        Assert.Equal(1, first.Deleted);
        Assert.DoesNotContain("daily/beninconnect-20260901.sql.gz", store.Objects.Keys);
        Assert.Contains("daily/beninconnect-20261101.sql.gz", store.Objects.Keys);
        Assert.Contains("notes/readme.txt", store.Objects.Keys);

        // Rien de nouveau : aucun envoi ; sauvegarde de la semaine remplacée (taille différente) : renvoyée
        Assert.Equal(0, (await mirror.SyncAsync(CancellationToken.None)).Uploaded);
        Write("weekly", "beninconnect-202645.sql.gz", 260);
        store.Uploads.Clear();
        Assert.Equal(1, (await mirror.SyncAsync(CancellationToken.None)).Uploaded);
        Assert.Equal(["weekly/beninconnect-202645.sql.gz"], store.Uploads);

        var local = LocalBackupFiles.List(_root);
        var status = await mirror.GetStatusAsync(local, CancellationToken.None);
        Assert.True(status.Enabled);
        Assert.NotNull(status.LastSyncAt);
        Assert.Contains(status.Remote, r => r is { Kind: "Daily", Name: "beninconnect-20261101.sql.gz", IsLocal: false });
        Assert.Contains(status.Remote, r => r is { Kind: "Manual", IsLocal: true });
        Assert.DoesNotContain(status.Remote, r => r.Name == "readme.txt");
        Assert.NotNull(await mirror.OpenAsync("Daily", "beninconnect-20261101.sql.gz", CancellationToken.None));
    }

    [Theory]
    [InlineData("daily/db-20261109.sql.gz", "Daily", "db-20261109.sql.gz")]
    [InlineData("media/photos-2026-11-09.tar.gz", "Media", "photos-2026-11-09.tar.gz")]
    [InlineData("other/db.sql.gz", null, null)]
    [InlineData("daily/../secret.sql.gz", null, null)]
    [InlineData("daily/notes.txt", null, null)]
    public void Keys_are_parsed_strictly(string key, string? kind, string? name)
    {
        var parsed = BackupMirror.Parse(key);
        Assert.Equal(kind, parsed?.Kind);
        Assert.Equal(name, parsed?.Name);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
