using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BeninConnect.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AdminTests(ApiFactory factory)
{
    private const string AdminPassword = "Admin2026!";

    private sealed record TokenBody(string Token);

    private sealed record Page(List<JsonElement> Items, int TotalCount);

    private async Task<HttpClient> AdminAsync(string email)
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = AdminPassword });
        response.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private async Task<HttpClient> MemberAsync(string email, string password = "Demo123!")
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    /// <summary>Membre jetable : inscrit puis vérifié avec le code fixe de développement.</summary>
    private async Task<(HttpClient Http, Guid Id, string Email)> NewMemberAsync()
    {
        var email = $"test.{Guid.NewGuid():N}@exemple.test";
        var http = factory.CreateClient();
        (await http.PostAsJsonAsync("api/auth/register", new { firstName = "Test", lastName = "Membre", email, country = "France", city = "Lyon", password = "Motdepasse1" }))
            .EnsureSuccessStatusCode();
        var verified = await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" });
        verified.EnsureSuccessStatusCode();
        var body = await verified.Content.ReadFromJsonAsync<JsonElement>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return (http, body.GetProperty("user").GetProperty("id").GetGuid(), email);
    }

    [Fact]
    public async Task Member_and_admin_tokens_are_not_interchangeable()
    {
        var member = await MemberAsync("demo@beninconnect.test");
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("api/admin/stats")).StatusCode);

        var admin = await AdminAsync("superadmin@beninconnect.test");
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("api/feed")).StatusCode);

        var wrong = await factory.CreateClient().PostAsJsonAsync("api/admin/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
    }

    [Theory]
    [InlineData("compta@beninconnect.test", "api/admin/exports/members", HttpStatusCode.OK)]
    [InlineData("compta@beninconnect.test", "api/admin/members", HttpStatusCode.Forbidden)]
    [InlineData("moderation@beninconnect.test", "api/admin/reports", HttpStatusCode.OK)]
    [InlineData("moderation@beninconnect.test", "api/admin/admins", HttpStatusCode.Forbidden)]
    [InlineData("moderation@beninconnect.test", "api/admin/exports/members", HttpStatusCode.Forbidden)]
    [InlineData("dev@beninconnect.test", "api/admin/system", HttpStatusCode.OK)]
    [InlineData("dev@beninconnect.test", "api/admin/posts", HttpStatusCode.Forbidden)]
    [InlineData("admin@beninconnect.test", "api/admin/communities", HttpStatusCode.OK)]
    [InlineData("admin@beninconnect.test", "api/admin/system", HttpStatusCode.Forbidden)]
    [InlineData("superadmin@beninconnect.test", "api/admin/admins", HttpStatusCode.OK)]
    public async Task Each_role_only_reaches_its_permissions(string email, string path, HttpStatusCode expected)
    {
        var admin = await AdminAsync(email);
        Assert.Equal(expected, (await admin.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Me_returns_role_and_permissions_and_stats_are_filled()
    {
        var admin = await AdminAsync("compta@beninconnect.test");
        var me = await admin.GetFromJsonAsync<JsonElement>("api/admin/auth/me");
        Assert.Equal("Accountant", me.GetProperty("role").GetString());
        Assert.Equal(["DashboardView", "ExportsDownload"], me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToArray());

        var stats = await admin.GetFromJsonAsync<JsonElement>("api/admin/stats");
        Assert.True(stats.GetProperty("members").GetInt32() >= 40);
        Assert.Equal(30, stats.GetProperty("signups").GetArrayLength());

        var csv = await admin.GetStringAsync("api/admin/exports/members");
        Assert.StartsWith("﻿Prénom;Nom;Email", csv);
    }

    [Fact]
    public async Task Suspended_member_is_locked_out_until_reactivated()
    {
        var (memberHttp, memberId, email) = await NewMemberAsync();
        var moderator = await AdminAsync("moderation@beninconnect.test");

        (await moderator.PostAsJsonAsync($"api/admin/members/{memberId}/suspend", new { reason = "Spam répété" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await memberHttp.GetAsync("api/feed")).StatusCode);
        var login = await factory.CreateClient().PostAsJsonAsync("api/auth/login", new { email, password = "Motdepasse1" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);

        (await moderator.PostAsync($"api/admin/members/{memberId}/reactivate", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await memberHttp.GetAsync("api/feed?skip=0&take=1")).StatusCode);
    }

    [Fact]
    public async Task Reported_post_can_be_hidden_and_is_then_invisible_to_members()
    {
        var (author, _, _) = await NewMemberAsync();
        var post = await (await author.PostAsJsonAsync("api/posts", new { content = "Publication à signaler pour le test", visibility = "Public" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var postId = post.GetProperty("id").GetGuid();

        var reporter = await MemberAsync("demo@beninconnect.test");
        Assert.Equal(HttpStatusCode.Created, (await reporter.PostAsJsonAsync("api/reports", new { targetType = "Post", targetId = postId, reason = "Spam" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reporter.PostAsJsonAsync("api/reports", new { targetType = "Post", targetId = postId, reason = "Spam" })).StatusCode);

        var moderator = await AdminAsync("moderation@beninconnect.test");
        var open = await moderator.GetFromJsonAsync<Page>("api/admin/reports?status=Open&take=100");
        var report = open!.Items.First(r => r.GetProperty("targetId").GetGuid() == postId);
        Assert.Equal("Publication à signaler pour le test", report.GetProperty("targetPreview").GetString());

        var resolved = await moderator.PostAsJsonAsync($"api/admin/reports/{report.GetProperty("id").GetGuid()}/resolve", new { decision = "hide", note = "Spam" });
        resolved.EnsureSuccessStatusCode();
        Assert.Equal("Resolved", (await resolved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await reporter.GetAsync($"api/posts/{postId}")).StatusCode);

        var audit = await (await AdminAsync("superadmin@beninconnect.test")).GetFromJsonAsync<Page>("api/admin/audit?search=masquée");
        Assert.Contains(audit!.Items, e => e.GetProperty("targetId").GetGuid() == postId);
    }

    [Fact]
    public async Task Admin_accounts_are_managed_by_superadmin_with_safeguards()
    {
        var super = await AdminAsync("superadmin@beninconnect.test");
        var me = await super.GetFromJsonAsync<JsonElement>("api/admin/auth/me");
        var meId = me.GetProperty("id").GetGuid();

        var self = await super.PutAsJsonAsync($"api/admin/admins/{meId}", new { displayName = "Aïcha Super", role = "Admin", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);

        var weak = await super.PostAsJsonAsync("api/admin/admins", new { email = "nouveau@beninconnect.test", displayName = "Nouveau", role = "Moderator", password = "court" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var email = $"mod.{Guid.NewGuid():N}@beninconnect.test";
        var created = await super.PostAsJsonAsync("api/admin/admins", new { email, displayName = "Nouvelle modératrice", role = "Moderator", password = AdminPassword });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var newcomer = await AdminAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await newcomer.GetAsync("api/admin/reports")).StatusCode);

        (await super.PutAsJsonAsync($"api/admin/admins/{id}", new { displayName = "Nouvelle modératrice", role = "Moderator", isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await newcomer.GetAsync("api/admin/reports")).StatusCode);
    }
}
