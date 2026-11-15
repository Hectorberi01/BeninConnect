using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BeninConnect.Api.Tests;

/// <summary>Couverture d'une communauté : image proposée ou photo envoyée, modifiable par le créateur et les modérateurs.</summary>
[Collection(ApiCollection.Name)]
public sealed class CommunityCoverTests(ApiFactory factory)
{
    private sealed record CommunityView(Guid Id, string? CoverImage);

    private sealed record MediaView(string Path);

    private async Task<HttpClient> MemberAsync(string? email = null)
    {
        var http = factory.CreateClient();
        if (email is null)
        {
            email = $"test.{Guid.NewGuid():N}@exemple.test";
            (await http.PostAsJsonAsync("api/auth/register", new { firstName = "Test", lastName = "Membre", email, country = "France", city = "Lyon", password = "Motdepasse1" }))
                .EnsureSuccessStatusCode();
            var verified = await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" });
            var token = (await verified.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return http;
        }

        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Demo123!" });
        var body = await login.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return http;
    }

    private static object NewCommunity(string? cover) => new
    {
        name = $"Couverture {Guid.NewGuid().ToString("N")[..8]}", description = "Une communauté pour tester la couverture.",
        category = "Entraide", country = "France", city = "Paris", visibility = "Public", coverImage = cover,
    };

    [Fact]
    public async Task Owner_uploads_a_cover_at_creation_and_changes_it_later()
    {
        var owner = await MemberAsync("demo@beninconnect.test");
        var form = new MultipartFormDataContent { { new ByteArrayContent(BannersTests.Png(1600, 900)), "file", "couverture.png" } };
        var upload = (await (await owner.PostAsync("api/media", form)).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<MediaView>())!;

        var created = await owner.PostAsJsonAsync("api/communities", NewCommunity(upload.Path));
        var community = (await created.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<CommunityView>())!;
        Assert.Equal(upload.Path, community.CoverImage);

        var changed = await owner.PutAsJsonAsync($"api/communities/{community.Id}/cover", new { coverImage = "Covers/community_02.jpg" });
        Assert.Equal("Covers/community_02.jpg", (await changed.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<CommunityView>())!.CoverImage);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PutAsJsonAsync($"api/communities/{community.Id}/cover", new { coverImage = "uploads/../../etc/passwd" })).StatusCode);

        var other = await MemberAsync();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await other.PutAsJsonAsync($"api/communities/{community.Id}/cover", new { coverImage = "Covers/community_03.jpg" })).StatusCode);
    }

    [Fact]
    public async Task An_invalid_cover_is_refused_at_creation()
    {
        var owner = await MemberAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("api/communities", NewCommunity("https://exemple.com/x.jpg"))).StatusCode);
    }
}
