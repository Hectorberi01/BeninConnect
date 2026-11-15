using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BeninConnect.Api.Tests;

/// <summary>Bannières de l'accueil : gérées dans la console, seules celles en ligne sont envoyées à l'application.</summary>
[Collection(ApiCollection.Name)]
public sealed class BannersTests(ApiFactory factory)
{
    private sealed record TokenBody(string Token);

    private sealed record ActionView(string Label, string Icon, string Target);

    private sealed record BannerView(Guid Id, string Title, string? Highlight, string Status, int Position, List<ActionView> Actions);

    private async Task<HttpClient> AdminAsync(string email = "admin@beninconnect.test")
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        response.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private async Task<HttpClient> MemberAsync()
    {
        var http = factory.CreateClient();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" });
        login.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return http;
    }

    private static object Banner(string title, bool active = true, DateTimeOffset? starts = null, DateTimeOffset? ends = null, object[]? actions = null,
        string? highlight = null, string layout = "Illustration") => new
    {
        title, highlight, subtitle = "Sous-titre", imagePath = (string?)null, layout, theme = "Green",
        actions = actions ?? [new { label = "Voir les trajets", icon = "airplane", target = "voyages" }],
        isActive = active, startsAt = starts, endsAt = ends,
    };

    private static async Task<BannerView> CreateAsync(HttpClient admin, object body)
    {
        var response = await admin.PostAsJsonAsync("api/admin/banners", body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BannerView>())!;
    }

    [Fact]
    public async Task Admin_manages_banners_and_the_app_only_sees_live_ones()
    {
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];
        var live = await CreateAsync(admin, Banner($"En ligne {tag}", highlight: "ligne",
            actions: [new { label = "Communautés", icon = "account-group-outline", target = "communities" }, new { label = "Site", icon = "web", target = "https://monbenin.com" }]));
        var hidden = await CreateAsync(admin, Banner($"Masquée {tag}", active: false));
        var later = await CreateAsync(admin, Banner($"Programmée {tag}", starts: DateTimeOffset.UtcNow.AddDays(2)));
        var ended = await CreateAsync(admin, Banner($"Terminée {tag}", starts: DateTimeOffset.UtcNow.AddDays(-3), ends: DateTimeOffset.UtcNow.AddDays(-1)));
        Assert.Equal(["Live", "Hidden", "Scheduled", "Ended"], new[] { live, hidden, later, ended }.Select(b => b.Status));

        var member = await MemberAsync();
        var seen = (await member.GetFromJsonAsync<List<BannerView>>("api/banners"))!;
        Assert.Contains(seen, b => b.Id == live.Id && b.Highlight == "ligne" && b.Actions.Count == 2);
        Assert.DoesNotContain(seen, b => b.Id == hidden.Id || b.Id == later.Id || b.Id == ended.Id);

        // Modification : masquée, elle disparaît de l'application
        var update = await admin.PutAsJsonAsync($"api/admin/banners/{live.Id}", Banner($"En ligne {tag}", active: false));
        Assert.Equal("Hidden", (await update.Content.ReadFromJsonAsync<BannerView>())!.Status);
        Assert.DoesNotContain((await member.GetFromJsonAsync<List<BannerView>>("api/banners"))!, b => b.Id == live.Id);

        // Ordre : la dernière passe en premier
        var all = (await admin.GetFromJsonAsync<List<BannerView>>("api/admin/banners"))!;
        var order = all.Select(b => b.Id).Where(id => id != ended.Id).Prepend(ended.Id).ToList();
        var reordered = (await (await admin.PutAsJsonAsync("api/admin/banners/order", new { ids = order })).Content.ReadFromJsonAsync<List<BannerView>>())!;
        Assert.Equal(ended.Id, reordered[0].Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("api/admin/banners/order", new { ids = order.Skip(1) })).StatusCode);

        foreach (var b in new[] { live, hidden, later, ended })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"api/admin/banners/{b.Id}")).StatusCode);
        }

        var audit = await admin.GetFromJsonAsync<JsonElement>($"api/admin/audit?targetId={live.Id}");
        Assert.Contains("Bannière supprimée", audit.GetRawText());
    }

    [Fact]
    public async Task Invalid_banners_are_refused_and_only_allowed_admins_manage_them()
    {
        var admin = await AdminAsync();
        foreach (var bad in new[]
                 {
                     Banner("ab"),
                     Banner("Titre correct", highlight: "absent"),
                     Banner("Titre correct", actions: [new { label = "Lien", icon = "web", target = "http://exemple.com" }]),
                     Banner("Titre correct", actions: [new { label = "Écran", icon = "web", target = "admin.secret" }]),
                     Banner("Titre correct", actions: [new { label = "Icône", icon = "<script>", target = "jobs" }]),
                     Banner("Titre correct", layout: "Background"),
                     Banner("Titre correct", starts: DateTimeOffset.UtcNow.AddDays(2), ends: DateTimeOffset.UtcNow.AddDays(1)),
                     Banner("Titre correct", actions: Enumerable.Range(0, 5).Select(i => (object)new { label = $"B{i}", icon = "web", target = "jobs" }).ToArray()),
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("api/admin/banners", bad)).StatusCode);
        }

        var moderator = await AdminAsync("moderation@beninconnect.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await moderator.GetAsync("api/admin/banners")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("api/banners")).StatusCode);
    }
}
