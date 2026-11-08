using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BeninConnect.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AccountDeletionTests(ApiFactory factory)
{
    private sealed record TokenView(string Token);

    private sealed record IdView(Guid Id);

    private sealed record PostView(Guid Id, Guid AuthorId, int CommentCount, List<Guid> LikedBy);

    private const string Password = "Secret123!";

    private async Task<(HttpClient Http, Guid Id, string Email)> NewMemberAsync()
    {
        var http = factory.CreateClient();
        var email = $"suppr.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = "Afi", lastName = "Test", email, country = "Bénin", city = "Cotonou", password = Password })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        await SignInAsync(http, email, Password);
        return (http, (await http.GetFromJsonAsync<IdView>("api/auth/me"))!.Id, email);
    }

    private static async Task<HttpClient> SignInAsync(HttpClient http, string email, string password)
    {
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenView>())!.Token);
        return http;
    }

    [Fact]
    public async Task Only_the_author_can_delete_a_post()
    {
        var (author, _, _) = await NewMemberAsync();
        var (other, _, _) = await NewMemberAsync();
        var post = (await (await author.PostAsJsonAsync("api/posts", new { content = "À supprimer bientôt", visibility = "Public" }))
            .Content.ReadFromJsonAsync<IdView>())!;

        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"api/posts/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await author.DeleteAsync($"api/posts/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await author.GetAsync($"api/posts/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await author.DeleteAsync($"api/posts/{post.Id}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_an_account_erases_the_member_and_their_content()
    {
        var demo = await SignInAsync(factory.CreateClient(), "demo@beninconnect.test", "Demo123!");
        var demoId = (await demo.GetFromJsonAsync<IdView>("api/auth/me"))!.Id;
        var demoPost = (await demo.GetFromJsonAsync<List<PostView>>($"api/posts?authorId={demoId}"))![0];

        var (member, memberId, email) = await NewMemberAsync();
        var own = (await (await member.PostAsJsonAsync("api/posts", new { content = "Ma publication", visibility = "Public" }))
            .Content.ReadFromJsonAsync<IdView>())!;
        (await member.PostAsJsonAsync($"api/posts/{demoPost.Id}/comments", new { content = "Mon commentaire" })).EnsureSuccessStatusCode();
        (await member.PostAsync($"api/posts/{demoPost.Id}/like", null)).EnsureSuccessStatusCode();
        var community = (await (await member.PostAsJsonAsync("api/communities", new
        {
            name = $"Groupe {Guid.NewGuid():N}"[..20], description = "Communauté de test à supprimer", category = "Culture",
            country = "Bénin", city = "Cotonou", visibility = "Public"
        })).Content.ReadFromJsonAsync<IdView>())!;
        var conversation = (await (await member.PostAsJsonAsync("api/conversations/direct", new { otherUserId = demoId }))
            .Content.ReadFromJsonAsync<IdView>())!;
        (await member.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { content = "Bonjour !" })).EnsureSuccessStatusCode();
        var commentsBefore = (await demo.GetFromJsonAsync<PostView>($"api/posts/{demoPost.Id}"))!.CommentCount;

        // Mauvais mot de passe : refusé
        Assert.Equal(HttpStatusCode.BadRequest, (await member.PostAsJsonAsync("api/users/me/delete", new { password = "Faux123!" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsJsonAsync("api/users/me/delete", new { password = Password })).StatusCode);

        // Le compte n'existe plus
        Assert.Equal(HttpStatusCode.NotFound, (await demo.GetAsync($"api/users/{memberId}")).StatusCode);
        Assert.False((await factory.CreateClient().PostAsJsonAsync("api/auth/login", new { email, password = Password })).IsSuccessStatusCode);

        // Ses contenus ont disparu
        Assert.Equal(HttpStatusCode.NotFound, (await demo.GetAsync($"api/posts/{own.Id}")).StatusCode);
        var after = (await demo.GetFromJsonAsync<PostView>($"api/posts/{demoPost.Id}"))!;
        Assert.Equal(commentsBefore - 1, after.CommentCount);
        Assert.DoesNotContain(memberId, after.LikedBy);
        Assert.Equal(HttpStatusCode.NotFound, (await demo.GetAsync($"api/communities/{community.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await demo.GetAsync($"api/conversations/{conversation.Id}")).StatusCode);
    }
}
