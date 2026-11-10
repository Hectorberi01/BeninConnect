using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace BeninConnect.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class SharePageTests(ApiFactory factory)
{
    private sealed record TokenView(string Token);

    private async Task<HttpClient> MemberAsync()
    {
        var http = factory.CreateClient();
        var email = $"partage.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = "Afi", lastName = "Partage", email, country = "France", city = "Lyon", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Secret123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenView>())!.Token);
        return http;
    }

    [Fact]
    public async Task Public_post_has_a_shareable_page_with_open_graph_tags()
    {
        var member = await MemberAsync();
        var post = await (await member.PostAsJsonAsync("api/posts", new { content = "Fête de l'indépendance à Lyon <ce samedi> !", visibility = "Public" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = post.GetProperty("id").GetGuid();
        var mine = await (await member.PostAsJsonAsync("api/posts", new { content = "Note privée", visibility = "OnlyMe" }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var anonymous = factory.CreateClient();
        var page = await anonymous.GetAsync($"post/{id}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("<meta property=\"og:title\" content=\"Afi Partage sur B", html);
        Assert.Contains($"https://monbenin.com/post/{id}", html);
        Assert.Contains($"beninconnect://post/{id}", html);
        Assert.Contains("apps.apple.com", html);
        Assert.Contains("play.google.com", html);
        Assert.DoesNotContain("<ce samedi>", html); // contenu échappé

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"post/{mine.GetProperty("id").GetGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"post/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task App_link_files_are_served_only_when_configured()
    {
        var off = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await off.GetAsync(".well-known/apple-app-site-association")).StatusCode);

        var on = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("AppLinks:AppleTeamId", "ABCDE12345");
            b.UseSetting("AppLinks:AndroidCertSha256", "AA:BB:CC");
        }).CreateClient();
        var apple = await on.GetFromJsonAsync<JsonElement>(".well-known/apple-app-site-association");
        Assert.Equal("ABCDE12345.com.hbatech.beninconnect.rn",
            apple.GetProperty("applinks").GetProperty("details")[0].GetProperty("appIDs")[0].GetString());
        var android = await on.GetFromJsonAsync<JsonElement>(".well-known/assetlinks.json");
        Assert.Equal("com.hbatech.beninconnect.rn", android[0].GetProperty("target").GetProperty("package_name").GetString());
        Assert.Equal("AA:BB:CC", android[0].GetProperty("target").GetProperty("sha256_cert_fingerprints")[0].GetString());
    }
}
