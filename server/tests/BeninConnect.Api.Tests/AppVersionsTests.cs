using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeninConnect.Modules.AppVersionManagement.Domain;

namespace BeninConnect.Api.Tests;

/// <summary>Versions de l'application : statut renvoyé à l'app, parc installé, relances validées par l'équipe.</summary>
[Collection(ApiCollection.Name)]
public sealed class AppVersionsTests(ApiFactory factory)
{
    private sealed record TokenBody(string Token);

    private sealed record StatusView(string CurrentVersion, string? LatestVersion, string? MinimumSupportedVersion, string Status, bool ForceUpdate, string StoreUrl);

    private sealed record CampaignView(Guid Id, string Status, int RecipientCount, int RequiredCount, int SentCount, int SkippedCount);

    private sealed record RecipientView(Guid UserId, string Status, bool Required, string CurrentVersion);

    private sealed record Detected(CampaignView? Campaign);

    private sealed record CampaignDetail(CampaignView Campaign, List<RecipientView> Recipients);

    private sealed record NotificationView(string Title, string Body);

    private sealed record InstallationView(Guid InstallationId, Guid? UserId, string Version, string Status);

    private sealed record Page<T>(List<T> Items, int TotalCount);

    private async Task<HttpClient> AdminAsync(string email = "superadmin@beninconnect.test")
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        response.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private async Task<(HttpClient Http, Guid Id)> MemberAsync()
    {
        var email = $"version.{Guid.NewGuid():N}@exemple.test";
        var http = factory.CreateClient();
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = "Test", lastName = "Version", email, country = "Bénin", city = "Cotonou", password = "Motdepasse1" })).EnsureSuccessStatusCode();
        var verified = await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" });
        var body = await verified.Content.ReadFromJsonAsync<JsonElement>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return (http, body.GetProperty("user").GetProperty("id").GetGuid());
    }

    private static async Task<StatusView> ReportAsync(HttpClient http, Guid installation, string version, string platform = "ios")
    {
        var response = await http.PostAsJsonAsync("api/app/installations",
            new { installationId = installation, platform, version, buildNumber = "42", osVersion = "18.1" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StatusView>())!;
    }

    /// <summary>iOS : dernière 1.3.0, minimum 1.1.0 ; Android : 1.3.0 sans minimum.</summary>
    private async Task<HttpClient> ReleasesAsync()
    {
        var admin = await AdminAsync();
        (await admin.PutAsJsonAsync("api/admin/app-versions/releases/ios",
            new { latestVersion = "1.3.0", minimumSupportedVersion = "1.1.0", storeUrl = "https://apps.apple.com/app/id6832112483" })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("api/admin/app-versions/releases/android",
            new { latestVersion = "1.3.0", minimumSupportedVersion = (string?)null, storeUrl = "https://play.google.com/store/apps/details?id=com.hbatech.beninconnect.rn" }))
            .EnsureSuccessStatusCode();
        return admin;
    }

    [Fact]
    public async Task App_receives_its_update_status()
    {
        var admin = await ReleasesAsync();
        var anonymous = factory.CreateClient();

        var available = await ReportAsync(anonymous, Guid.NewGuid(), "1.2.0");
        Assert.Equal("UPDATE_AVAILABLE", available.Status);
        Assert.False(available.ForceUpdate);
        Assert.Equal("1.3.0", available.LatestVersion);
        Assert.Equal("1.1.0", available.MinimumSupportedVersion);
        Assert.StartsWith("https://apps.apple.com/", available.StoreUrl);

        var required = await ReportAsync(anonymous, Guid.NewGuid(), "1.0.9");
        Assert.Equal("UPDATE_REQUIRED", required.Status);
        Assert.True(required.ForceUpdate);
        Assert.Equal("UP_TO_DATE", (await ReportAsync(anonymous, Guid.NewGuid(), "1.10.0")).Status); // 1.10 > 1.3
        Assert.Equal("UPDATE_AVAILABLE", (await ReportAsync(anonymous, Guid.NewGuid(), "1.0.0", "android")).Status); // pas de minimum Android

        foreach (var bad in new object[]
                 {
                     new { installationId = Guid.NewGuid(), platform = "windows", version = "1.0.0" },
                     new { installationId = Guid.NewGuid(), platform = "ios", version = "1.0.0; drop" },
                     new { installationId = Guid.Empty, platform = "ios", version = "1.0.0" },
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("api/app/installations", bad)).StatusCode);
        }

        // Ancienne route : minimum de la plateforme, ou aucun si une plateforme n'en a pas
        Assert.Equal("1.1.0", (await anonymous.GetFromJsonAsync<JsonElement>("api/app/version?platform=ios")).GetProperty("minimumVersion").GetString());
        Assert.Equal(JsonValueKind.Null, (await anonymous.GetFromJsonAsync<JsonElement>("api/app/version")).GetProperty("minimumVersion").ValueKind);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("api/admin/app-versions/releases/ios",
            new { latestVersion = "1.0.0", minimumSupportedVersion = "1.2.0", storeUrl = "https://apps.apple.com/x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await AdminAsync("moderation@beninconnect.test")).GetAsync("api/admin/app-versions")).StatusCode);
    }

    [Fact]
    public async Task Installation_is_attached_to_the_connected_member_only_from_the_token()
    {
        var admin = await ReleasesAsync();
        var (member, id) = await MemberAsync();
        var installation = Guid.NewGuid();
        await ReportAsync(factory.CreateClient(), installation, "1.0.0"); // avant connexion
        await ReportAsync(member, installation, "1.0.0");
        await ReportAsync(factory.CreateClient(), installation, "1.0.0"); // déconnecté : reste rattachée

        var page = (await admin.GetFromJsonAsync<Page<InstallationView>>($"api/admin/app-versions/installations?userId={id}"))!;
        var row = Assert.Single(page.Items);
        Assert.Equal(installation, row.InstallationId);
        Assert.Equal("UpdateRequired", row.Status);

        var required = (await admin.GetFromJsonAsync<Page<InstallationView>>("api/admin/app-versions/installations?status=UpdateRequired&take=100"))!;
        Assert.Contains(required.Items, i => i.InstallationId == installation);
        Assert.All(required.Items, i => Assert.Equal("UpdateRequired", i.Status));

        var overview = await admin.GetFromJsonAsync<JsonElement>("api/admin/app-versions");
        Assert.True(overview.GetProperty("members").GetProperty("updateRequired").GetInt32() >= 1);
        Assert.Contains(overview.GetProperty("versions").EnumerateArray(), v => v.GetProperty("version").GetString() == "1.0.0");
    }

    [Fact]
    public async Task Outdated_members_are_notified_only_after_approval()
    {
        var admin = await ReleasesAsync();
        var (late, lateId) = await MemberAsync();
        var (updated, updatedId) = await MemberAsync();
        var (upToDate, upToDateId) = await MemberAsync();
        await ReportAsync(late, Guid.NewGuid(), "1.0.0");
        await ReportAsync(updated, Guid.NewGuid(), "1.2.0");
        var reinstall = Guid.NewGuid();
        await ReportAsync(upToDate, Guid.NewGuid(), "1.0.0");
        await ReportAsync(upToDate, reinstall, "1.3.0"); // réinstallée : seule la plus récente compte

        var campaign = (await (await admin.PostAsync("api/admin/app-versions/detect", null)).Content.ReadFromJsonAsync<Detected>())!.Campaign!;
        Assert.Equal("PendingApproval", campaign.Status);
        var detail = (await admin.GetFromJsonAsync<CampaignDetail>($"api/admin/app-versions/campaigns/{campaign.Id}"))!;
        Assert.Contains(detail.Recipients, r => r.UserId == lateId && r.Required);
        Assert.Contains(detail.Recipients, r => r.UserId == updatedId && !r.Required);
        Assert.DoesNotContain(detail.Recipients, r => r.UserId == upToDateId);

        // Rien n'est envoyé avant validation
        Assert.DoesNotContain((await late.GetFromJsonAsync<List<NotificationView>>("api/notifications"))!, n => n.Title == "Mise à jour obligatoire");

        await ReportAsync(updated, Guid.NewGuid(), "1.3.0"); // à jour entre la détection et l'envoi
        var approved = await admin.PostAsJsonAsync($"api/admin/app-versions/campaigns/{campaign.Id}/approve", new { sendPush = true, sendEmail = true });
        approved.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync($"api/admin/app-versions/campaigns/{campaign.Id}/reject", new { reason = "trop tard" })).StatusCode);

        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync("api/admin/background-jobs/app-versions.send-campaigns/run", null)).StatusCode);
        CampaignDetail sent = null!;
        for (var i = 0; i < 100; i++)
        {
            sent = (await admin.GetFromJsonAsync<CampaignDetail>($"api/admin/app-versions/campaigns/{campaign.Id}"))!;
            if (sent.Campaign.Status == "Sent")
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.Equal("Sent", sent.Campaign.Status);
        Assert.Equal("Sent", sent.Recipients.Single(r => r.UserId == lateId).Status);
        Assert.Equal("Skipped", sent.Recipients.Single(r => r.UserId == updatedId).Status);
        var notice = (await late.GetFromJsonAsync<List<NotificationView>>("api/notifications"))!.Single(n => n.Title == "Mise à jour obligatoire");
        Assert.Contains("1.3.0", notice.Body);
        Assert.DoesNotContain((await updated.GetFromJsonAsync<List<NotificationView>>("api/notifications"))!, n => n.Title.Contains("version") || n.Title.Contains("Mise à jour"));

        // Relancé récemment : pas dans la détection suivante, qui peut être refusée
        var next = (await (await admin.PostAsync("api/admin/app-versions/detect", null)).Content.ReadFromJsonAsync<Detected>())!.Campaign;
        if (next is not null)
        {
            Assert.DoesNotContain((await admin.GetFromJsonAsync<CampaignDetail>($"api/admin/app-versions/campaigns/{next.Id}"))!.Recipients, r => r.UserId == lateId);
            (await admin.PostAsJsonAsync($"api/admin/app-versions/campaigns/{next.Id}/reject", new { reason = "Test" })).EnsureSuccessStatusCode();
        }
    }

    [Theory]
    [InlineData("1.2.0", "1.3.0", "1.1.0", UpdateStatus.UpdateAvailable)]
    [InlineData("1.0.9", "1.3.0", "1.1.0", UpdateStatus.UpdateRequired)]
    [InlineData("1.3", "1.3.0", "1.1.0", UpdateStatus.UpToDate)]
    [InlineData("1.10.0", "1.9.0", null, UpdateStatus.UpToDate)]
    [InlineData("2.0.0", null, "1.1.0", UpdateStatus.UpToDate)]
    [InlineData("1.0.0", null, null, UpdateStatus.Unknown)]
    [InlineData("abc", "1.3.0", null, UpdateStatus.Unknown)]
    public void Status_compares_versions_numerically(string version, string? latest, string? minimum, UpdateStatus expected) =>
        Assert.Equal(expected, VersionRules.StatusOf(version, latest, minimum));
}
