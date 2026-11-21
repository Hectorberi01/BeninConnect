using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BeninConnect.Api.Tests;

/// <summary>Réglages de confidentialité : enregistrés sur le serveur et appliqués à ce que voient les autres membres.</summary>
[Collection(ApiCollection.Name)]
public sealed class PrivacyTests(ApiFactory factory)
{
    private sealed record Privacy(bool PrivateProfile, bool ShowCity, bool AllowMessages);

    private sealed record User(Guid Id, string FirstName, string Email, string City, string Bio, string Profession, List<string> Interests);

    private async Task<(HttpClient Http, Guid Id)> MemberAsync(string city)
    {
        var email = $"test.{Guid.NewGuid():N}@exemple.test";
        var http = factory.CreateClient();
        (await http.PostAsJsonAsync("api/auth/register", new { firstName = "Privé", lastName = "Test", email, country = "France", city, password = "Motdepasse1" }))
            .EnsureSuccessStatusCode();
        var body = await (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        (await http.PutAsJsonAsync("api/users/me", new
        {
            firstName = "Privé", lastName = "Test", profession = "Infirmière", country = "France", city, bio = "Ma bio", interests = new[] { "Musique" },
        })).EnsureSuccessStatusCode();
        return (http, body.GetProperty("user").GetProperty("id").GetGuid());
    }

    private static async Task<Privacy> SetAsync(HttpClient http, bool privateProfile, bool showCity, bool allowMessages) =>
        (await (await http.PutAsJsonAsync("api/users/me/privacy", new { privateProfile, showCity, allowMessages })).EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<Privacy>())!;

    [Fact]
    public async Task Settings_are_saved_and_hide_details_from_other_members()
    {
        var city = $"Ville{Guid.NewGuid():N}"[..14];
        var (alice, aliceId) = await MemberAsync(city);
        var (bob, _) = await MemberAsync("Lyon");

        Assert.Equal(new Privacy(false, true, true), await alice.GetFromJsonAsync<Privacy>("api/users/me/privacy"));

        // Profil public : tout est visible sauf l'email
        var seen = (await bob.GetFromJsonAsync<User>($"api/users/{aliceId}"))!;
        Assert.Equal(("", city, "Ma bio"), (seen.Email, seen.City, seen.Bio));
        Assert.Contains(await bob.GetFromJsonAsync<List<User>>($"api/users?search={city}") ?? [], u => u.Id == aliceId);

        // Ville masquée
        Assert.Equal(new Privacy(false, false, true), await SetAsync(alice, false, false, true));
        Assert.Equal("", (await bob.GetFromJsonAsync<User>($"api/users/{aliceId}"))!.City);
        Assert.DoesNotContain(await bob.GetFromJsonAsync<List<User>>($"api/users?search={city}") ?? [], u => u.Id == aliceId);

        // Profil privé : détails masqués aux non-contacts, visibles après une discussion
        await SetAsync(alice, true, true, true);
        seen = (await bob.GetFromJsonAsync<User>($"api/users/{aliceId}"))!;
        Assert.Equal(("", "", "", 0), (seen.City, seen.Bio, seen.Profession, seen.Interests.Count));
        Assert.Equal("Privé", seen.FirstName);
        (await bob.PostAsJsonAsync("api/conversations/direct", new { otherUserId = aliceId })).EnsureSuccessStatusCode();
        seen = (await bob.GetFromJsonAsync<User>($"api/users/{aliceId}"))!;
        Assert.Equal((city, "Ma bio", "Infirmière"), (seen.City, seen.Bio, seen.Profession));

        // Le membre voit toujours tout de son propre profil
        var me = (await alice.GetFromJsonAsync<User>($"api/users/{aliceId}"))!;
        Assert.Equal((city, "Ma bio"), (me.City, me.Bio));
        Assert.NotEmpty(me.Email);
    }

    [Fact]
    public async Task Members_who_refuse_messages_cannot_be_contacted_by_new_people()
    {
        var (alice, aliceId) = await MemberAsync("Paris");
        var (bob, _) = await MemberAsync("Lyon");
        var (carol, _) = await MemberAsync("Nice");

        var existing = await (await bob.PostAsJsonAsync("api/conversations/direct", new { otherUserId = aliceId })).EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<JsonElement>();
        await SetAsync(alice, false, true, false);

        var refused = await carol.PostAsJsonAsync("api/conversations/direct", new { otherUserId = aliceId });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("n'accepte pas de nouveaux messages", await refused.Content.ReadAsStringAsync());

        // Une discussion existante continue
        var again = await (await bob.PostAsJsonAsync("api/conversations/direct", new { otherUserId = aliceId })).EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(existing.GetProperty("id").GetGuid(), again.GetProperty("id").GetGuid());
    }
}
