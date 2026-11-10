using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BeninConnect.Api.Tests;

/// <summary>Suppression d'une notification et d'une discussion (pour moi seulement).</summary>
[Collection(ApiCollection.Name)]
public sealed class CleanupTests(ApiFactory factory)
{
    private sealed record TokenView(string Token);

    private sealed record IdView(Guid Id);

    private sealed record CountView(int Count);

    private sealed record NotificationView(Guid Id, string Title);

    private sealed record MessageView(Guid Id, string Content);

    private sealed record SummaryView(ConversationRef Conversation, MessageView? LastMessage, int UnreadCount);

    private sealed record ConversationRef(Guid Id);

    private async Task<(HttpClient Http, Guid Id)> MemberAsync()
    {
        var http = factory.CreateClient();
        var email = $"nettoyage.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = "Codjo", lastName = "Test", email, country = "Bénin", city = "Cotonou", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Secret123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenView>())!.Token);
        return (http, (await http.GetFromJsonAsync<IdView>("api/auth/me"))!.Id);
    }

    private static async Task<List<NotificationView>> NotificationsAsync(HttpClient http, int atLeast)
    {
        for (var i = 0; ; i++)
        {
            var list = (await http.GetFromJsonAsync<List<NotificationView>>("api/notifications"))!;
            if (list.Count >= atLeast || i == 40)
            {
                return list;
            }

            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task Notifications_can_be_deleted_one_by_one_or_all_at_once()
    {
        var (me, _) = await MemberAsync();
        var (other, _) = await MemberAsync();
        var post = (await (await me.PostAsJsonAsync("api/posts", new { content = "Bonjour", visibility = "Public" })).Content.ReadFromJsonAsync<IdView>())!;
        for (var i = 0; i < 3; i++)
        {
            (await other.PostAsJsonAsync($"api/posts/{post.Id}/comments", new { content = $"Commentaire {i}" })).EnsureSuccessStatusCode();
        }

        var list = await NotificationsAsync(me, 3);
        Assert.True(list.Count >= 3);

        (await me.DeleteAsync($"api/notifications/{list[0].Id}")).EnsureSuccessStatusCode();
        Assert.DoesNotContain(await NotificationsAsync(me, 0), n => n.Id == list[0].Id);

        // Celle d'un autre membre : introuvable
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"api/notifications/{list[1].Id}")).StatusCode);

        var cleared = await (await me.DeleteAsync("api/notifications")).Content.ReadFromJsonAsync<CountView>();
        Assert.Equal(list.Count - 1, cleared!.Count);
        Assert.Empty(await NotificationsAsync(me, 0));
        Assert.Equal(0, (await me.GetFromJsonAsync<CountView>("api/notifications/unread-count"))!.Count);
    }

    [Fact]
    public async Task Deleting_a_conversation_hides_it_for_me_only_until_a_new_message()
    {
        var (me, _) = await MemberAsync();
        var (other, otherId) = await MemberAsync();
        var conversation = (await (await me.PostAsJsonAsync("api/conversations/direct", new { otherUserId = otherId })).Content.ReadFromJsonAsync<IdView>())!;
        (await me.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { content = "Salut" })).EnsureSuccessStatusCode();
        (await other.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { content = "Bonjour !" })).EnsureSuccessStatusCode();

        (await me.DeleteAsync($"api/conversations/{conversation.Id}")).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await me.GetFromJsonAsync<List<SummaryView>>("api/conversations"))!, s => s.Conversation.Id == conversation.Id);
        Assert.Empty((await me.GetFromJsonAsync<List<MessageView>>($"api/conversations/{conversation.Id}/messages"))!);
        Assert.Equal(0, (await me.GetFromJsonAsync<CountView>("api/conversations/unread-count"))!.Count);

        // L'autre membre garde tout l'historique
        Assert.Equal(2, (await other.GetFromJsonAsync<List<MessageView>>($"api/conversations/{conversation.Id}/messages"))!.Count);

        // Nouveau message : la discussion revient, avec ce message seulement
        await Task.Delay(20);
        (await other.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { content = "Tu es là ?" })).EnsureSuccessStatusCode();
        var summary = (await me.GetFromJsonAsync<List<SummaryView>>("api/conversations"))!.Single(s => s.Conversation.Id == conversation.Id);
        Assert.Equal("Tu es là ?", summary.LastMessage!.Content);
        Assert.Equal(1, summary.UnreadCount);
        Assert.Equal(["Tu es là ?"], (await me.GetFromJsonAsync<List<MessageView>>($"api/conversations/{conversation.Id}/messages"))!.Select(m => m.Content));

        // Membre extérieur à la discussion : introuvable
        var (stranger, _) = await MemberAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"api/conversations/{conversation.Id}")).StatusCode);
    }
}
