using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BeninConnect.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class BackupAndMemberTests(ApiFactory factory)
{
    private sealed record TokenBody(string Token);

    private sealed record FileView(string Kind, string Name, long Size, bool CanDelete);

    private sealed record Overview(bool DirectoryAvailable, bool CanCreate, string? PgDumpVersion, List<FileView> Files);

    private async Task<HttpClient> AdminAsync(string email)
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        response.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    [Fact]
    public async Task Backups_are_listed_downloaded_and_manual_ones_deleted()
    {
        Directory.CreateDirectory(Path.Combine(factory.BackupDirectory, "daily"));
        Directory.CreateDirectory(Path.Combine(factory.BackupDirectory, "manual"));
        await File.WriteAllTextAsync(Path.Combine(factory.BackupDirectory, "daily", "beninconnect-20261108.sql.gz"), "auto");
        await File.WriteAllTextAsync(Path.Combine(factory.BackupDirectory, "manual", "beninconnect-20261109-101500.sql.gz"), "manuelle");
        await File.WriteAllTextAsync(Path.Combine(factory.BackupDirectory, "daily", "notes.txt"), "ignoré");

        var admin = await AdminAsync("superadmin@beninconnect.test");
        var overview = (await admin.GetFromJsonAsync<Overview>("api/admin/backups"))!;
        Assert.True(overview.DirectoryAvailable);
        Assert.Contains(overview.Files, f => f is { Kind: "Daily", Name: "beninconnect-20261108.sql.gz", CanDelete: false });
        Assert.Contains(overview.Files, f => f is { Kind: "Manual", CanDelete: true });
        Assert.DoesNotContain(overview.Files, f => f.Name == "notes.txt");

        var download = await admin.GetAsync("api/admin/backups/Daily/beninconnect-20261108.sql.gz");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("auto", await download.Content.ReadAsStringAsync());

        // Chemins détournés et sauvegardes automatiques : refusés
        Assert.NotEqual(HttpStatusCode.OK, (await admin.GetAsync("api/admin/backups/Daily/..%2F..%2Fetc%2Fpasswd")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("api/admin/backups/Other/x.sql.gz")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.DeleteAsync("api/admin/backups/Daily/beninconnect-20261108.sql.gz")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("api/admin/backups/Manual/beninconnect-20261109-101500.sql.gz")).StatusCode);
        Assert.False(File.Exists(Path.Combine(factory.BackupDirectory, "manual", "beninconnect-20261109-101500.sql.gz")));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("api/admin/backups/Manual/beninconnect-20261109-101500.sql.gz")).StatusCode);

        // Rôle sans la permission
        var moderator = await AdminAsync("moderation@beninconnect.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await moderator.GetAsync("api/admin/backups")).StatusCode);

        // Sauvegarde réelle seulement si pg_dump est compatible avec le serveur de test
        if (Environment.GetEnvironmentVariable("BC_TEST_PGDUMP") == "1")
        {
            var created = await admin.PostAsync("api/admin/backups", null);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            var file = (await created.Content.ReadFromJsonAsync<FileView>())!;
            Assert.True(file.Size > 1000);
            Assert.True(File.Exists(Path.Combine(factory.BackupDirectory, "manual", file.Name)));
        }
    }

    [Fact]
    public async Task Member_detail_shows_activity_and_admin_history()
    {
        var admin = await AdminAsync("superadmin@beninconnect.test");
        var page = await admin.GetFromJsonAsync<JsonElement>("api/admin/members?take=1");
        var id = page.GetProperty("items")[0].GetProperty("id").GetGuid();

        (await admin.PostAsJsonAsync($"api/admin/members/{id}/suspend", new { reason = "Test de la fiche membre" })).EnsureSuccessStatusCode();
        (await admin.PostAsync($"api/admin/members/{id}/reactivate", null)).EnsureSuccessStatusCode();

        var detail = await admin.GetFromJsonAsync<JsonElement>($"api/admin/members/{id}");
        foreach (var field in new[] { "memberships", "events", "messaging", "reportsAbout", "devices", "interests" })
        {
            Assert.True(detail.TryGetProperty(field, out _), field);
        }

        Assert.True(detail.GetProperty("messaging").GetProperty("conversations").GetInt32() >= 0);

        var audit = await admin.GetFromJsonAsync<JsonElement>($"api/admin/audit?targetId={id}");
        var actions = audit.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("action").GetString()).ToList();
        Assert.Contains("Membre suspendu", actions);
        Assert.Contains("Membre réactivé", actions);
        Assert.All(audit.GetProperty("items").EnumerateArray(), e => Assert.Equal(id, e.GetProperty("targetId").GetGuid()));
    }
}
