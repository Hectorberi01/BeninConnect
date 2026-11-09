using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BeninConnect.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class BlockTests(ApiFactory factory)
{
    private sealed record TokenView(string Token);

    private sealed record IdView(Guid Id);

    private sealed record SummaryView(IdView Conversation, bool IsBlocked);

    private async Task<(HttpClient Http, Guid Id)> NewMemberAsync()
    {
        var http = factory.CreateClient();
        var email = $"bloc.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = "Kodjo", lastName = "Test", email, country = "Bénin", city = "Cotonou", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Secret123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenView>())!.Token);
        return (http, (await http.GetFromJsonAsync<IdView>("api/auth/me"))!.Id);
    }

    [Fact]
    public async Task Blocking_hides_content_and_stops_messages_both_ways()
    {
        var (me, meId) = await NewMemberAsync();
        var (other, otherId) = await NewMemberAsync();
        var post = (await (await other.PostAsJsonAsync("api/posts", new { content = "Publication à masquer", visibility = "Public" }))
            .Content.ReadFromJsonAsync<IdView>())!;
        var mine = (await (await me.PostAsJsonAsync("api/posts", new { content = "Ma publication", visibility = "Public" }))
            .Content.ReadFromJsonAsync<IdView>())!;
        (await other.PostAsJsonAsync($"api/posts/{mine.Id}/comments", new { content = "Commentaire à masquer" })).EnsureSuccessStatusCode();
        var conversation = (await (await me.PostAsJsonAsync("api/conversations/direct", new { otherUserId = otherId }))
            .Content.ReadFromJsonAsync<IdView>())!;

        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsync($"api/users/{meId}/block", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await me.PostAsync($"api/users/{otherId}/block", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await me.PostAsync($"api/users/{otherId}/block", null)).StatusCode); // idempotent

        var blocked = await me.GetFromJsonAsync<List<IdView>>("api/users/me/blocked");
        Assert.Equal([otherId], blocked!.Select(u => u.Id));

        // Contenus masqués dans les deux sens
        Assert.Equal(HttpStatusCode.NotFound, (await me.GetAsync($"api/posts/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"api/posts/{mine.Id}")).StatusCode);
        Assert.Empty((await me.GetFromJsonAsync<List<IdView>>($"api/posts/{mine.Id}/comments"))!);

        // Plus de messages, dans les deux sens
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { content = "Salut" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { content = "Salut" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("api/conversations/direct", new { otherUserId = meId })).StatusCode);
        Assert.True((await other.GetFromJsonAsync<SummaryView>($"api/conversations/{conversation.Id}"))!.IsBlocked);

        // Déblocage : tout revient
        Assert.Equal(HttpStatusCode.NoContent, (await me.DeleteAsync($"api/users/{otherId}/block")).StatusCode);
        Assert.Empty((await me.GetFromJsonAsync<List<IdView>>("api/users/me/blocked"))!);
        Assert.Equal(HttpStatusCode.OK, (await me.GetAsync($"api/posts/{post.Id}")).StatusCode);
        Assert.Single((await me.GetFromJsonAsync<List<IdView>>($"api/posts/{mine.Id}/comments"))!);
        (await other.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { content = "Re-bonjour" })).EnsureSuccessStatusCode();
        Assert.False((await me.GetFromJsonAsync<SummaryView>($"api/conversations/{conversation.Id}"))!.IsBlocked);
    }
}
