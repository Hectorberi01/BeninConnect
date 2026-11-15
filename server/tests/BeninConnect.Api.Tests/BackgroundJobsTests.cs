using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeninConnect.Modules.Administration.Application;
using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Notifications.Application;
using BeninConnect.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Api.Tests;

/// <summary>Tâches planifiées : liste et lancement manuel depuis la console, tâches de nettoyage.</summary>
[Collection(ApiCollection.Name)]
public sealed class BackgroundJobsTests(ApiFactory factory)
{
    private sealed record TokenBody(string Token);

    private sealed record RunView(DateTimeOffset StartedAt, string Status, string Summary, string Trigger);

    private sealed record JobView(string Key, string Name, bool Enabled, bool Scheduled, bool Running, RunView? LastRun);

    private sealed record Overview(bool SchedulerEnabled, List<JobView> Jobs);

    private async Task<HttpClient> AdminAsync(string email)
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        response.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    /// <summary>Lance la tâche et attend la fin de cette exécution.</summary>
    private static async Task<RunView> RunAsync(HttpClient admin, string key)
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync($"api/admin/background-jobs/{key}/run", null)).StatusCode);
        for (var i = 0; i < 100; i++)
        {
            var job = (await admin.GetFromJsonAsync<Overview>("api/admin/background-jobs"))!.Jobs.Single(j => j.Key == key);
            if (!job.Running && job.LastRun is { } run && run.StartedAt >= before)
            {
                return run;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"{key} ne s'est pas terminée.");
    }

    [Fact]
    public async Task Developer_and_superadmin_see_every_job_others_are_refused()
    {
        var dev = await AdminAsync("dev@beninconnect.test");
        var overview = (await dev.GetFromJsonAsync<Overview>("api/admin/background-jobs"))!;
        Assert.False(overview.SchedulerEnabled); // désactivé dans les tests
        var keys = overview.Jobs.Select(j => j.Key).ToList();
        foreach (var key in new[]
                 {
                     "administration.daily-backup", "administration.backup-verify", "administration.audit-retention", "jobs.import",
                     "services.review-reminders", "voyages.trip-lifecycle", "notifications.purge", "identity.accounts-cleanup", "media.orphans",
                 })
        {
            Assert.Contains(key, keys);
        }

        Assert.DoesNotContain("review.reset", keys); // environnement de revue seulement
        Assert.DoesNotContain("administration.backup-mirror", overview.Jobs.Where(j => j.Enabled).Select(j => j.Key)); // pas de bucket

        Assert.Equal(HttpStatusCode.OK, (await (await AdminAsync("superadmin@beninconnect.test")).GetAsync("api/admin/background-jobs")).StatusCode);
        var admin = await AdminAsync("admin@beninconnect.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("api/admin/background-jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync("api/admin/background-jobs/notifications.purge/run", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dev.PostAsync("api/admin/background-jobs/inconnue/run", null)).StatusCode);
        // Copie distante non configurée : refusée
        Assert.Equal(HttpStatusCode.Conflict, (await dev.PostAsync("api/admin/background-jobs/administration.backup-mirror/run", null)).StatusCode);
    }

    [Fact]
    public async Task Notifications_purge_removes_old_notifications_and_inactive_devices()
    {
        var user = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<INotificationsDb>();
            db.Notifications.AddRange(
                Notification.Create(Guid.NewGuid(), user, NotificationCategory.System, "lue ancienne", "", now.AddDays(-100), isRead: true),
                Notification.Create(Guid.NewGuid(), user, NotificationCategory.System, "non lue", "", now.AddDays(-100)),
                Notification.Create(Guid.NewGuid(), user, NotificationCategory.System, "très ancienne", "", now.AddDays(-200)),
                Notification.Create(Guid.NewGuid(), user, NotificationCategory.System, "récente lue", "", now.AddDays(-5), isRead: true));
            db.PushDevices.Add(PushDevice.Register(Guid.NewGuid(), user, $"ExponentPushToken[{Guid.NewGuid():N}]", "ios", [], now.AddDays(-200)));
            await db.SaveChangesAsync();
        }

        var run = await RunAsync(await AdminAsync("dev@beninconnect.test"), "notifications.purge");
        Assert.Equal("Succeeded", run.Status);
        Assert.StartsWith("Manuelle : ", run.Trigger);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<INotificationsDb>();
            var left = await db.Notifications.Where(n => n.UserId == user).Select(n => n.Title).OrderBy(t => t).ToListAsync();
            Assert.Equal(["non lue", "récente lue"], left);
            Assert.False(await db.PushDevices.AnyAsync(d => d.UserId == user));
        }
    }

    [Fact]
    public async Task Accounts_cleanup_deletes_old_unverified_accounts_and_expired_reset_codes()
    {
        var http = factory.CreateClient();
        var old = $"jamais.verifie.{Guid.NewGuid():N}@exemple.test";
        var fresh = $"recent.{Guid.NewGuid():N}@exemple.test";
        foreach (var email in new[] { old, fresh })
        {
            (await http.PostAsJsonAsync("api/auth/register",
                new { firstName = "Test", lastName = "Membre", email, country = "Bénin", city = "Cotonou", password = "Motdepasse1" })).EnsureSuccessStatusCode();
        }

        (await http.PostAsJsonAsync("api/auth/forgot-password", new { email = "demo@beninconnect.test" })).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IIdentityDb>();
            await db.Users.Where(u => u.Email == old).ExecuteUpdateAsync(s => s.SetProperty(u => u.JoinedAt, DateTimeOffset.UtcNow.AddDays(-40)));
            await db.Users.Where(u => u.Email == "demo@beninconnect.test")
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordResetExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var run = await RunAsync(await AdminAsync("superadmin@beninconnect.test"), "identity.accounts-cleanup");
        Assert.Equal("Succeeded", run.Status);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IIdentityDb>();
            Assert.False(await db.Users.AnyAsync(u => u.Email == old));
            Assert.True(await db.Users.AnyAsync(u => u.Email == fresh));
            var demo = await db.Users.AsNoTracking().SingleAsync(u => u.Email == "demo@beninconnect.test");
            Assert.Null(demo.PasswordResetCode);
            Assert.True(demo.IsEmailVerified);
        }
    }

    [Fact]
    public async Task Audit_retention_removes_entries_older_than_twelve_months()
    {
        var dev = await AdminAsync("dev@beninconnect.test");
        await RunAsync(dev, "media.orphans"); // ajoute une entrée « lancée manuellement »

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAdministrationDb>();
            var entry = await db.AuditEntries.OrderByDescending(e => e.At).FirstAsync(e => e.Action == "Tâche planifiée lancée manuellement");
            await db.AuditEntries.Where(e => e.Id == entry.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.At, DateTimeOffset.UtcNow.AddMonths(-13)));

            var run = await RunAsync(dev, "administration.audit-retention");
            Assert.Equal("Succeeded", run.Status);
            Assert.False(await db.AuditEntries.AnyAsync(e => e.Id == entry.Id));
            Assert.True(await db.AuditEntries.AnyAsync(e => e.Action == "Tâche planifiée lancée manuellement")); // celle de la rétention
        }
    }

    [Fact]
    public async Task Orphan_media_only_reports_by_default()
    {
        var run = await RunAsync(await AdminAsync("dev@beninconnect.test"), "media.orphans");
        Assert.Equal("Succeeded", run.Status);
        Assert.Contains("rapport seulement", run.Summary);
    }
}
