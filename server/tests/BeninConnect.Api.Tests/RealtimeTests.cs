using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeninConnect.Modules.Messaging.Application;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Api.Tests;

/// <summary>Modification de publications et de messages, diffusion temps réel (SignalR).</summary>
[Collection(ApiCollection.Name)]
public sealed class RealtimeTests(ApiFactory factory)
{
    private sealed record TokenView(string Token);

    private sealed record IdView(Guid Id);

    private sealed record PostView(Guid Id, string Content, string Visibility, DateTimeOffset? EditedAt);

    private sealed record MessageView(Guid Id, string Content, bool IsDeleted, DateTimeOffset? EditedAt, List<object> Reactions);

    private sealed class FakeNotifier : IChatNotifier
    {
        public ConcurrentQueue<(string Event, IReadOnlyCollection<Guid> To, Guid Id)> Sent { get; } = new();

        public Task MessageCreatedAsync(IReadOnlyCollection<Guid> userIds, MessageDto message, CancellationToken ct) => Add("created", userIds, message.Id);

        public Task MessageUpdatedAsync(IReadOnlyCollection<Guid> userIds, MessageDto message, CancellationToken ct) => Add("updated", userIds, message.Id);

        public Task ConversationReadAsync(IReadOnlyCollection<Guid> userIds, Guid conversationId, Guid readerId, CancellationToken ct) =>
            Add("read", userIds, conversationId);

        private Task Add(string name, IReadOnlyCollection<Guid> to, Guid id)
        {
            Sent.Enqueue((name, to, id));
            return Task.CompletedTask;
        }
    }

    private static async Task<(HttpClient Http, Guid Id, string Token)> MemberAsync(HttpClient http)
    {
        var email = $"rt.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = "Kossi", lastName = "Temps", email, country = "Bénin", city = "Cotonou", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Secret123!" });
        var token = (await login.Content.ReadFromJsonAsync<TokenView>())!.Token;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (http, (await http.GetFromJsonAsync<IdView>("api/auth/me"))!.Id, token);
    }

    [Fact]
    public async Task The_author_can_edit_a_post()
    {
        var (author, _, _) = await MemberAsync(factory.CreateClient());
        var (other, _, _) = await MemberAsync(factory.CreateClient());
        var post = (await (await author.PostAsJsonAsync("api/posts", new { content = "Première version", visibility = "Public" }))
            .Content.ReadFromJsonAsync<PostView>())!;
        Assert.Null(post.EditedAt);

        var edit = new { content = "Version corrigée", imagePath = (string?)null, visibility = "OnlyMe" };
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"api/posts/{post.Id}", edit)).StatusCode);
        var edited = (await (await author.PutAsJsonAsync($"api/posts/{post.Id}", edit)).Content.ReadFromJsonAsync<PostView>())!;
        Assert.Equal("Version corrigée", edited.Content);
        Assert.Equal("OnlyMe", edited.Visibility);
        Assert.NotNull(edited.EditedAt);
        Assert.Equal(HttpStatusCode.BadRequest, (await author.PutAsJsonAsync($"api/posts/{post.Id}", new { content = "x", visibility = "Public" })).StatusCode);
    }

    [Fact]
    public async Task Messages_can_be_edited_and_deleted_by_their_sender_and_participants_are_notified()
    {
        var fake = new FakeNotifier();
        using var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IChatNotifier>(_ => fake)));
        var (alice, aliceId, _) = await MemberAsync(app.CreateClient());
        var (bob, bobId, _) = await MemberAsync(app.CreateClient());

        var conversation = (await (await alice.PostAsJsonAsync("api/conversations/direct", new { otherUserId = bobId }))
            .Content.ReadFromJsonAsync<IdView>())!;
        var path = $"api/conversations/{conversation.Id}/messages";
        var message = (await (await alice.PostAsJsonAsync(path, new { content = "Bonjour Bob" })).Content.ReadFromJsonAsync<MessageView>())!;
        Assert.Contains(fake.Sent, e => e.Event == "created" && e.Id == message.Id && e.To.Contains(bobId) && e.To.Contains(aliceId));

        // Seul l'expéditeur modifie ou supprime
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PutAsJsonAsync($"{path}/{message.Id}", new { content = "Piraté" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.DeleteAsync($"{path}/{message.Id}")).StatusCode);

        var edited = (await (await alice.PutAsJsonAsync($"{path}/{message.Id}", new { content = "Bonjour Bob !" })).Content.ReadFromJsonAsync<MessageView>())!;
        Assert.Equal("Bonjour Bob !", edited.Content);
        Assert.NotNull(edited.EditedAt);

        (await bob.PostAsJsonAsync($"{path}/{message.Id}/reactions", new { emoji = "👍" })).EnsureSuccessStatusCode();
        var deleted = (await (await alice.DeleteAsync($"{path}/{message.Id}")).Content.ReadFromJsonAsync<MessageView>())!;
        Assert.True(deleted.IsDeleted);
        Assert.Equal(string.Empty, deleted.Content);
        Assert.Empty(deleted.Reactions);
        Assert.Equal(3, fake.Sent.Count(e => e.Event == "updated" && e.Id == message.Id));

        // Vu par Bob : contenu effacé ; plus de modification possible
        var seen = (await bob.GetFromJsonAsync<List<MessageView>>(path))!.Single(m => m.Id == message.Id);
        Assert.True(seen.IsDeleted);
        Assert.Equal(string.Empty, seen.Content);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync($"{path}/{message.Id}", new { content = "Retour" })).StatusCode);

        // Lecture : l'expéditeur est prévenu
        (await alice.PostAsJsonAsync(path, new { content = "Tu es là ?" })).EnsureSuccessStatusCode();
        (await bob.PostAsync($"api/conversations/{conversation.Id}/read", null)).EnsureSuccessStatusCode();
        Assert.Contains(fake.Sent, e => e.Event == "read" && e.Id == conversation.Id && e.To.Contains(aliceId));
    }

    [Fact]
    public async Task The_chat_hub_requires_a_member_token_passed_in_the_query_string()
    {
        var http = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync("hubs/chat/negotiate?negotiateVersion=1", null)).StatusCode);

        var (_, _, token) = await MemberAsync(factory.CreateClient());
        var negotiate = await factory.CreateClient().PostAsync($"hubs/chat/negotiate?negotiateVersion=1&access_token={token}", null);
        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
        Assert.Contains("connectionToken", await negotiate.Content.ReadAsStringAsync());
    }
}
