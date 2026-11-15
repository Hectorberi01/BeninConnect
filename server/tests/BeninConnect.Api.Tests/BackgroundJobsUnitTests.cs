using System.IO.Compression;
using System.Text;
using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Administration.Infrastructure;
using BeninConnect.Modules.Media.Infrastructure;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Api.Tests;

public sealed class BackgroundJobsUnitTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("bc-jobs-").FullName;

    private static DateTimeOffset Utc(int day, int hour, int minute) => new(2026, 11, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void Daily_schedule_runs_at_the_next_occurrence_and_never_catches_up()
    {
        var daily = JobSchedule.DailyAt(new TimeOnly(3, 20));
        Assert.Equal(Utc(16, 3, 20), daily.First(Utc(16, 1, 0)));
        Assert.Equal(Utc(17, 3, 20), daily.First(Utc(16, 9, 0))); // heure passée : demain
        Assert.Equal(Utc(17, 3, 20), daily.Next(Utc(16, 3, 20)));
        Assert.Equal("Chaque jour à 03:20 UTC", daily.Describe());
    }

    [Fact]
    public void Weekly_and_interval_schedules()
    {
        var weekly = JobSchedule.WeeklyAt(DayOfWeek.Monday, new TimeOnly(4, 10)); // 16/11/2026 : lundi
        Assert.Equal(Utc(16, 4, 10), weekly.Next(Utc(15, 12, 0)));
        Assert.Equal(Utc(23, 4, 10), weekly.Next(Utc(16, 4, 10)));
        Assert.Equal("Chaque lundi à 04:10 UTC", weekly.Describe());

        var hourly = JobSchedule.Every(TimeSpan.FromHours(1), initialDelay: TimeSpan.FromMinutes(3));
        Assert.Equal(Utc(16, 1, 3), hourly.First(Utc(16, 1, 0)));
        Assert.Equal(Utc(16, 2, 3), hourly.Next(Utc(16, 1, 3)));
        Assert.Equal("Toutes les heures", hourly.Describe());
        Assert.Equal("Toutes les 10 minutes", JobSchedule.Every(TimeSpan.FromMinutes(10)).Describe());
        Assert.Equal("Chaque minute", JobSchedule.Every(TimeSpan.FromSeconds(5)).Describe()); // une minute au moins
    }

    private string Gzip(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Fastest);
        gzip.Write(Encoding.UTF8.GetBytes(content));
        return path;
    }

    [Fact]
    public async Task Backup_check_accepts_complete_dumps_and_rejects_truncated_or_corrupt_ones()
    {
        var big = string.Concat(Enumerable.Repeat("INSERT INTO t VALUES (1);\n", 20000));
        await BackupVerifyTask.CheckDumpAsync(Gzip("ok.sql.gz", big + "--\n-- PostgreSQL database dump complete\n--\n"), "ok", CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() => BackupVerifyTask.CheckDumpAsync(Gzip("partial.sql.gz", big), "partial", CancellationToken.None));

        var corrupt = Path.Combine(_dir, "corrupt.sql.gz");
        await File.WriteAllBytesAsync(corrupt, [0x1f, 0x8b, 8, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var error = await Assert.ThrowsAsync<DomainException>(() => BackupVerifyTask.CheckDumpAsync(corrupt, "corrupt", CancellationToken.None));
        Assert.Contains("corrompue", error.Message);
    }

    [Theory]
    [InlineData("uploads/a1.jpg", "a1.jpg")]
    [InlineData("/uploads/a1.jpg", "a1.jpg")]
    [InlineData("https://media.monbenin.com/uploads/a1.jpg?v=2", "a1.jpg")]
    [InlineData("a1.jpg", "a1.jpg")]
    public void Orphan_cleanup_compares_file_names(string path, string expected) => Assert.Equal(expected, MediaOrphansTask.FileName(path));

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
