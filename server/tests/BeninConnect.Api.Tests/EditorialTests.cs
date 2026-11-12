using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using BeninConnect.Modules.Editorial.Application;
using BeninConnect.Modules.Editorial.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Api.Tests;

/// <summary>Flux et LLM simulés : aucun appel réseau pendant les tests.</summary>
public sealed class FakeFeedReader : IFeedReader
{
    public List<FeedItem> Items { get; } = [];

    // Chaque source ne voit que les articles de son propre site
    public Task<IReadOnlyList<FeedItem>> ReadAsync(string url, int max, CancellationToken ct) =>
        url.Contains("panne", StringComparison.Ordinal)
            ? throw new HttpRequestException("Flux injoignable")
            : Task.FromResult<IReadOnlyList<FeedItem>>(Items.Where(i => SameSite(i.Url, url)).Take(max).ToList());

    private static string Site(string url) => new Uri(url).Host.Replace("www.", "", StringComparison.Ordinal);

    private static bool SameSite(string a, string b) => Site(a) == Site(b);
}

public sealed class FakeLlm : ILlmClient
{
    public Func<string, string> Answer { get; set; } = user => "Belle nouvelle pour la diaspora ! Qu'en pensez-vous ? #Bénin";

    public bool IsConfigured => true;

    public string Model => "faux-modele";

    public Task<string> CompleteAsync(string system, string user, CancellationToken ct) => Task.FromResult(Answer(user));
}

public sealed class EditorialFactory : ApiFactory
{
    public FakeFeedReader Feeds { get; } = new();

    public FakeLlm Llm { get; } = new();

    protected override void Configure(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(s =>
        {
            s.AddSingleton<IFeedReader>(Feeds);
            s.AddSingleton<ILlmClient>(Llm);
        });
}

public sealed class EditorialTests(EditorialFactory factory) : IClassFixture<EditorialFactory>
{
    private sealed record TokenBody(string Token);

    private sealed record Candidate(Guid Id, string Status, string SourceTitle, string? DraftContent, string? Reason, Guid? PublishedPostId, DateTimeOffset? ScheduledAt);

    private sealed record Page(List<Candidate> Items, int TotalCount);

    private sealed record Report(int Collected, int Drafted, int Skipped, int Published, int Failed);

    private sealed record Source(Guid Id, string Name, string? LastError, int CollectedCount);

    private sealed record Overview(bool AutomationEnabled, bool LlmConfigured, Guid OfficialAccountId, List<Source> Sources);

    private async Task<HttpClient> AdminAsync(string email = "superadmin@beninconnect.test")
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private static async Task<Report> RunAsync(HttpClient admin, string step)
    {
        var response = await admin.PostAsync($"api/admin/editorial/run/{step}", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Report>())!;
    }

    private static async Task<List<Candidate>> ListAsync(HttpClient admin, string status) =>
        (await admin.GetFromJsonAsync<Page>($"api/admin/editorial/candidates?status={status}&take=100"))!.Items;

    [Fact]
    public async Task Collected_articles_are_drafted_reviewed_then_published_by_the_official_account()
    {
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        (await admin.PostAsJsonAsync("api/admin/editorial/sources", new { name = $"Média {tag}", url = $"https://media-{tag}.bj/rss" })).EnsureSuccessStatusCode();

        factory.Feeds.Items.Clear();
        factory.Feeds.Items.AddRange([
            new FeedItem($"Festival {tag}", $"https://media-{tag}.bj/festival?utm_source=rss", "Le festival revient &amp; attire la diaspora."),
            new FeedItem($"Festival {tag} (doublon)", $"https://www.media-{tag}.bj/festival/", "Même article, autre adresse."),
            new FeedItem($"Politique {tag}", $"https://media-{tag}.bj/politique?id=2", "Débat partisan."),
        ]);
        factory.Llm.Answer = user => user.Contains("Politique", StringComparison.Ordinal) ? "SKIP." : "Le festival revient cette année ! Vous y serez ? #Bénin";

        Assert.Equal(2, (await RunAsync(admin, "collect")).Collected); // doublon écarté avant l'enregistrement
        Assert.Equal(0, (await RunAsync(admin, "collect")).Collected);  // déjà connu

        var drafts = await RunAsync(admin, "draft");
        Assert.True(drafts.Drafted >= 1 && drafts.Skipped >= 1);
        var draft = (await ListAsync(admin, "Drafted")).Single(c => c.SourceTitle == $"Festival {tag}");
        Assert.Contains($"📰 Média {tag} : https://media-{tag}.bj/festival?utm_source=rss", draft.DraftContent);
        Assert.Contains(await ListAsync(admin, "Skipped"), c => c.SourceTitle == $"Politique {tag}");

        // Rien ne part sans validation
        Assert.Equal(0, (await RunAsync(admin, "publish")).Published);

        var approved = await admin.PostAsJsonAsync($"api/admin/editorial/candidates/{draft.Id}/approve",
            new { content = draft.DraftContent + "\nRelu par l'équipe.", scheduledAt = (DateTimeOffset?)null });
        approved.EnsureSuccessStatusCode();
        Assert.True((await RunAsync(admin, "publish")).Published >= 1);

        var published = (await ListAsync(admin, "Published")).Single(c => c.Id == draft.Id);
        Assert.NotNull(published.PublishedPostId);

        // Publication visible dans le fil, au nom du compte officiel (badge)
        var overview = (await admin.GetFromJsonAsync<Overview>("api/admin/editorial"))!;
        var member = factory.CreateClient();
        var login = await member.PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" });
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        var post = await member.GetFromJsonAsync<JsonElement>($"api/posts/{published.PublishedPostId}");
        Assert.Equal(overview.OfficialAccountId, post.GetProperty("authorId").GetGuid());
        Assert.Contains("Relu par l'équipe.", post.GetProperty("content").GetString());
        var author = await member.GetFromJsonAsync<JsonElement>($"api/users/{overview.OfficialAccountId}");
        Assert.True(author.GetProperty("isOfficial").GetBoolean());
    }

    [Fact]
    public async Task A_failing_source_is_reported_without_blocking_the_others()
    {
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var broken = await (await admin.PostAsJsonAsync("api/admin/editorial/sources", new { name = $"En panne {tag}", url = $"https://panne-{tag}.bj/rss" }))
            .Content.ReadFromJsonAsync<Source>();
        factory.Feeds.Items.Clear();
        factory.Feeds.Items.Add(new FeedItem($"Article {tag}", $"https://ok-{tag}.bj/a", "Texte"));
        (await admin.PostAsJsonAsync("api/admin/editorial/sources", new { name = $"OK {tag}", url = $"https://ok-{tag}.bj/rss" })).EnsureSuccessStatusCode();

        Assert.True((await RunAsync(admin, "collect")).Collected >= 1);
        var sources = (await admin.GetFromJsonAsync<Overview>("api/admin/editorial"))!.Sources;
        Assert.Contains("injoignable", sources.Single(s => s.Id == broken!.Id).LastError);
        Assert.True(sources.Single(s => s.Name == $"OK {tag}").CollectedCount >= 1);
    }

    [Fact]
    public async Task Manual_posts_rejections_and_scheduling()
    {
        var admin = await AdminAsync();
        var draft = await (await admin.PostAsJsonAsync("api/admin/editorial/candidates", new { content = "Bonne fête de l'indépendance à toute la diaspora ! 🇧🇯" }))
            .Content.ReadFromJsonAsync<Candidate>();
        Assert.Equal("Drafted", draft!.Status);

        var later = DateTimeOffset.UtcNow.AddDays(2);
        var scheduled = await (await admin.PostAsJsonAsync($"api/admin/editorial/candidates/{draft.Id}/approve", new { content = draft.DraftContent, scheduledAt = later }))
            .Content.ReadFromJsonAsync<Candidate>();
        Assert.Equal("Approved", scheduled!.Status);
        await RunAsync(admin, "publish");
        Assert.DoesNotContain(await ListAsync(admin, "Published"), c => c.Id == draft.Id); // pas avant l'heure

        Assert.Equal("Drafted", (await (await admin.PostAsync($"api/admin/editorial/candidates/{draft.Id}/unschedule", null)).Content.ReadFromJsonAsync<Candidate>())!.Status);
        var rejected = await (await admin.PostAsJsonAsync($"api/admin/editorial/candidates/{draft.Id}/reject", new { reason = "Doublon" })).Content.ReadFromJsonAsync<Candidate>();
        Assert.Equal("Rejected", rejected!.Status);
        Assert.Equal("Doublon", rejected.Reason);

        var tooLong = await admin.PostAsJsonAsync("api/admin/editorial/candidates", new { content = new string('a', 2001) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Only_allowed_admins_reach_the_editorial_console_and_the_official_account_cannot_sign_in()
    {
        var accountant = await AdminAsync("compta@beninconnect.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("api/admin/editorial")).StatusCode);
        var moderator = await AdminAsync("moderation@beninconnect.test");
        Assert.Equal(HttpStatusCode.OK, (await moderator.GetAsync("api/admin/editorial")).StatusCode);

        var login = await factory.CreateClient().PostAsJsonAsync("api/auth/login", new { email = "officiel@monbenin.com", password = "nimporte-quoi" });
        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);

        // Personne ne lit les messages du compte officiel : discussion refusée
        var official = (await moderator.GetFromJsonAsync<Overview>("api/admin/editorial"))!.OfficialAccountId;
        var member = factory.CreateClient();
        var token = await (await member.PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" })).Content.ReadFromJsonAsync<TokenBody>();
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        var start = await member.PostAsJsonAsync("api/conversations/direct", new { otherUserId = official });
        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);
        Assert.Contains("ne reçoit pas de messages", await start.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Official_posts_can_carry_photos_uploaded_from_the_console()
    {
        var admin = await AdminAsync();
        string[] paths = new string[2];
        for (var i = 0; i < 2; i++)
        {
            // Signature PNG suivie de données : suffisant pour le contrôle du format
            var png = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 1, 2, 3]);
            png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            using var form = new MultipartFormDataContent { { png, "file", $"photo{i}.png" } };
            var upload = await admin.PostAsync("api/admin/editorial/images", form);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            paths[i] = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("path").GetString()!;
            Assert.StartsWith("uploads/", paths[i]);
        }

        using var notImage = new MultipartFormDataContent { { new StringContent("pas une image"), "file", "x.txt" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("api/admin/editorial/images", notImage)).StatusCode);
        var accountant = await AdminAsync("compta@beninconnect.test");
        using var form2 = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "x.png" } };
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsync("api/admin/editorial/images", form2)).StatusCode);

        var draft = await (await admin.PostAsJsonAsync("api/admin/editorial/candidates", new { content = "Nos plus belles photos de la semaine 📸 #Bénin", images = paths }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, draft.GetProperty("images").GetArrayLength());

        var id = draft.GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"api/admin/editorial/candidates/{id}/approve", new { content = "Nos plus belles photos de la semaine 📸 #Bénin", images = paths }))
            .EnsureSuccessStatusCode();
        await RunAsync(admin, "publish");
        var published = (await ListAsync(admin, "Published")).Single(c => c.Id == id);

        var member = factory.CreateClient();
        var token = await (await member.PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" })).Content.ReadFromJsonAsync<TokenBody>();
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        var post = await member.GetFromJsonAsync<JsonElement>($"api/posts/{published.PublishedPostId}");
        Assert.Equal(paths, post.GetProperty("images").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    [Fact]
    public void A_web_page_is_not_accepted_as_a_feed()
    {
        var html = XDocument.Parse("<html><head><title>Mon entreprise</title></head><body><a href='/a'>Lien</a></body></html>");
        var error = Assert.Throws<InvalidOperationException>(() => XmlFeedReader.Parse(html, "https://monentreprise.bj/", 10));
        Assert.Contains("pas un flux RSS", error.Message);
    }

    [Fact]
    public void Feed_parsing_handles_rss_atom_relative_links_and_entities()
    {
        var rss = XDocument.Parse("""
            <rss version="2.0"><channel>
              <item><title>Titre &amp;amp; plus</title><link>/actu/1</link><description><![CDATA[<p>Texte &eacute;l&#233;gant</p>]]></description></item>
              <item><title>Sans lien</title></item>
            </channel></rss>
            """);
        var items = XmlFeedReader.Parse(rss, "https://media.bj/rss", 10);
        Assert.Equal("https://media.bj/actu/1", Assert.Single(items).Url);
        Assert.Equal("Titre & plus", items[0].Title);
        Assert.Equal("Texte élégant", items[0].Summary);

        var atom = XDocument.Parse("""
            <feed xmlns="http://www.w3.org/2005/Atom"><entry><title>Atom</title>
              <link rel="alternate" href="https://media.bj/atom/1"/><summary>Résumé</summary></entry></feed>
            """);
        Assert.Equal("https://media.bj/atom/1", Assert.Single(XmlFeedReader.Parse(atom, "https://media.bj/feed", 10)).Url);

        Assert.Equal(SourceUrl.Hash("https://www.media.bj/a/?utm_source=x&id=2"), SourceUrl.Hash("https://media.bj/a?id=2"));
        Assert.NotEqual(SourceUrl.Hash("https://media.bj/a?id=1"), SourceUrl.Hash("https://media.bj/a?id=2"));
        Assert.True(EditorialPipeline.IsSkip("Skip."));
        Assert.False(EditorialPipeline.IsSkip("Skipper la file d'attente au festival ? Venez tôt, les places partent vite cette année ! #Bénin"));
        Assert.True(EditorialPipeline.WithSource(new string('a', 3000), "Média", "https://media.bj/1").Length <= 2000);
    }
}
