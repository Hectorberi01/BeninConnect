using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BeninConnect.Api.Tests;

/// <summary>Support : un membre écrit au compte officiel, l'équipe lit et répond depuis la console.</summary>
[Collection(ApiCollection.Name)]
public sealed class SupportTests(ApiFactory factory)
{
    private sealed record Conversation(Guid Id, List<Guid> ParticipantIds);

    private sealed record Message(Guid Id, Guid SenderId, string Content, bool IsRead);

    private sealed record Member(Guid Id, string FirstName);

    private sealed record Summary(Guid Id, Member Member, Message? LastMessage, int UnreadCount);

    private sealed record Thread(Guid Id, Member Member, Guid OfficialId, List<Message> Messages);

    private async Task<(HttpClient Http, Guid Id)> NewMemberAsync()
    {
        var email = $"test.{Guid.NewGuid():N}@exemple.test";
        var http = factory.CreateClient();
        (await http.PostAsJsonAsync("api/auth/register", new { firstName = "Support", lastName = "Test", email, country = "France", city = "Lyon", password = "Motdepasse1" }))
            .EnsureSuccessStatusCode();
        var body = await (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return (http, body.GetProperty("user").GetProperty("id").GetGuid());
    }

    private async Task<HttpClient> AdminAsync(string email)
    {
        var http = factory.CreateClient();
        var login = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await login.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return http;
    }

    [Fact]
    public async Task Member_writes_to_support_and_the_team_answers_from_the_console()
    {
        var (member, memberId) = await NewMemberAsync();
        var conversation = (await (await member.PostAsync("api/conversations/support", null)).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<Conversation>())!;
        Assert.Contains(memberId, conversation.ParticipantIds);
        (await member.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { content = "Je n'arrive pas à publier une photo." }))
            .EnsureSuccessStatusCode();

        // La discussion du membre apparaît, non lue, dans la console
        var moderator = await AdminAsync("moderation@beninconnect.test");
        var unread = await moderator.GetFromJsonAsync<JsonElement>("api/admin/support/unread-count");
        Assert.True(unread.GetProperty("count").GetInt32() >= 1);
        var list = (await moderator.GetFromJsonAsync<List<Summary>>("api/admin/support?unread=true"))!;
        var summary = Assert.Single(list, s => s.Id == conversation.Id);
        Assert.Equal((memberId, 1), (summary.Member.Id, summary.UnreadCount));
        Assert.Equal("Je n'arrive pas à publier une photo.", summary.LastMessage!.Content);

        var thread = (await moderator.GetFromJsonAsync<Thread>($"api/admin/support/{conversation.Id}"))!;
        Assert.Single(thread.Messages);

        // Réponse au nom du compte officiel : lue côté console, reçue par le membre
        var reply = await moderator.PostAsJsonAsync($"api/admin/support/{conversation.Id}/messages", new { content = "Bonjour ! Pouvez-vous réessayer en Wi-Fi ?" });
        var sent = (await reply.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<Message>())!;
        Assert.Equal(thread.OfficialId, sent.SenderId);
        Assert.DoesNotContain((await moderator.GetFromJsonAsync<List<Summary>>("api/admin/support?unread=true"))!, s => s.Id == conversation.Id);

        var messages = (await member.GetFromJsonAsync<List<Message>>($"api/conversations/{conversation.Id}/messages"))!;
        Assert.Equal(["Je n'arrive pas à publier une photo.", "Bonjour ! Pouvez-vous réessayer en Wi-Fi ?"], messages.Select(m => m.Content));
        var notifications = await member.GetFromJsonAsync<JsonElement>("api/notifications");
        Assert.Contains("Nouveau message de BéninConnect", notifications.GetRawText());

        var audit = await (await AdminAsync("superadmin@beninconnect.test")).GetFromJsonAsync<JsonElement>($"api/admin/audit?targetId={memberId}");
        Assert.Contains("Réponse au support", audit.GetRawText());
    }

    [Fact]
    public async Task Only_support_conversations_are_reachable_and_only_by_allowed_roles()
    {
        var (member, _) = await NewMemberAsync();
        var (other, otherId) = await NewMemberAsync();
        var direct = (await (await member.PostAsJsonAsync("api/conversations/direct", new { otherUserId = otherId })).EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<Conversation>())!;

        var moderator = await AdminAsync("moderation@beninconnect.test");
        Assert.Equal(HttpStatusCode.NotFound, (await moderator.GetAsync($"api/admin/support/{direct.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await moderator.PostAsJsonAsync($"api/admin/support/{direct.Id}/messages", new { content = "Intrusion" })).StatusCode);

        var accountant = await AdminAsync("compta@beninconnect.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("api/admin/support")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("api/admin/support")).StatusCode);
    }

    [Fact]
    public async Task The_team_writes_first_to_a_member_from_the_console()
    {
        var (member, memberId) = await NewMemberAsync();
        var moderator = await AdminAsync("moderation@beninconnect.test");

        var started = await moderator.PostAsJsonAsync("api/admin/support/start", new { userId = memberId, content = "Bienvenue sur BéninConnect !" });
        var thread = (await started.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<Thread>())!;
        Assert.Equal(memberId, thread.Member.Id);
        Assert.Equal(thread.OfficialId, Assert.Single(thread.Messages).SenderId);

        // Même discussion au deuxième message ; le membre la voit avec ses messages non lus
        var again = (await (await moderator.PostAsJsonAsync("api/admin/support/start", new { userId = memberId, content = "Pensez à compléter votre profil." }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<Thread>())!;
        Assert.Equal((thread.Id, 2), (again.Id, again.Messages.Count));
        var conversations = await member.GetFromJsonAsync<JsonElement>("api/conversations");
        var summary = conversations.EnumerateArray().Single(c => c.GetProperty("conversation").GetProperty("id").GetGuid() == thread.Id);
        Assert.Equal(2, summary.GetProperty("unreadCount").GetInt32());

        Assert.Equal(HttpStatusCode.NotFound, (await moderator.PostAsJsonAsync("api/admin/support/start", new { userId = Guid.NewGuid(), content = "Bonjour" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await moderator.PostAsJsonAsync("api/admin/support/start", new { userId = thread.OfficialId, content = "Bonjour" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await moderator.PostAsJsonAsync("api/admin/support/start", new { userId = memberId, content = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await (await AdminAsync("compta@beninconnect.test")).PostAsJsonAsync("api/admin/support/start", new { userId = memberId, content = "Bonjour" })).StatusCode);
    }
}
