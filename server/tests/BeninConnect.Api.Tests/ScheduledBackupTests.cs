using BeninConnect.Modules.Administration.Application;
using BeninConnect.Modules.Administration.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BeninConnect.Api.Tests;

/// <summary>Sauvegarde quotidienne faite par l'API (pg_dump réel : BC_TEST_PGDUMP=1 et BC_TEST_POSTGRES).</summary>
public sealed class ScheduledBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"bc-scheduled-{Guid.NewGuid():N}");

    [Fact]
    public async Task Daily_backup_fills_weekly_and_monthly_and_rotates_old_files()
    {
        var server = Environment.GetEnvironmentVariable("BC_TEST_POSTGRES");
        if (Environment.GetEnvironmentVariable("BC_TEST_PGDUMP") != "1" || string.IsNullOrEmpty(server))
        {
            return; // pg_dump compatible requis
        }

        Directory.CreateDirectory(Path.Combine(_root, "daily"));
        var old = Path.Combine(_root, "daily", "postgres-20261020.sql.gz");
        await File.WriteAllTextAsync(old, "ancienne");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-10));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = $"{server};Database=postgres",
        }).Build();
        var store = new BackupStore(new BackupSettings { Directory = _root }, config, TimeProvider.System, NullLogger<BackupStore>.Instance);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.False(store.HasDailyBackup(today));
        var file = await store.CreateScheduledAsync(CancellationToken.None);

        Assert.True(store.HasDailyBackup(today));
        Assert.Equal("Daily", file.Kind);
        Assert.True(file.Size > 100);
        Assert.False(File.Exists(old)); // plus de 7 jours : supprimée
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "weekly")));
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "monthly"), $"postgres-{DateTime.UtcNow:yyyyMM}.sql.gz"));

        // Deuxième passage le même jour : fichiers remplacés, pas dupliqués
        await store.CreateScheduledAsync(CancellationToken.None);
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "daily")));
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "weekly")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
