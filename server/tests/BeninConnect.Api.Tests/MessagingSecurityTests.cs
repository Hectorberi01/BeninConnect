using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using BeninConnect.Modules.Messaging.Infrastructure;
using BeninConnect.Modules.Messaging.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Api.Tests;

/// <summary>Réponses citées, chiffrement des messages en base et reprise des anciens messages.</summary>
public sealed class MessagingSecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record TokenBody(string Token);

    private sealed record IdBody(Guid Id);

    private sealed record Reply(Guid Id, Guid SenderId, string Excerpt, bool IsDeleted);

    private sealed record Attachment(string FileName);

    private sealed record Msg(Guid Id, string Content, Reply? ReplyTo, bool IsDeleted, Attachment? Attachment);

    private sealed record Status(int TotalMessages, int PlainMessages, bool Running, int Processed, string? Error);

    private async Task<(HttpClient Http, Guid Id)> MemberAsync(string first)
    {
        var http = factory.CreateClient();
        var email = $"msg.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = first, lastName = "Test", email, country = "France", city = "Paris", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Secret123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return (http, (await http.GetFromJsonAsync<IdBody>("api/auth/me"))!.Id);
    }

    private async Task<HttpClient> AdminAsync(string email)
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private async Task<(HttpClient A, HttpClient B, Guid Conversation)> ConversationAsync()
    {
        var (a, _) = await MemberAsync("Ama");
        var (b, bId) = await MemberAsync("Bio");
        var conversation = await (await a.PostAsJsonAsync("api/conversations/direct", new { otherUserId = bId })).Content.ReadFromJsonAsync<IdBody>();
        return (a, b, conversation!.Id);
    }

    private static async Task<Msg> SendAsync(HttpClient http, Guid conversation, string content, Guid? replyToId = null)
    {
        var response = await http.PostAsJsonAsync($"api/conversations/{conversation}/messages", new { content, replyToId });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<Msg>())!;
    }

    private async Task<string> StoredContentAsync(Guid messageId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
        return await db.Database.SqlQuery<string>($"SELECT \"Content\" AS \"Value\" FROM messaging.messages WHERE \"Id\" = {messageId}").SingleAsync();
    }

    [Fact]
    public async Task Messages_are_encrypted_at_rest_and_readable_through_the_api()
    {
        var (a, b, conversation) = await ConversationAsync();
        var sent = await SendAsync(a, conversation, "Mon code d'accès est 4321");

        var stored = await StoredContentAsync(sent.Id);
        Assert.StartsWith("enc1:", stored);
        Assert.DoesNotContain("4321", stored);

        var messages = await b.GetFromJsonAsync<List<Msg>>($"api/conversations/{conversation}/messages");
        Assert.Equal("Mon code d'accès est 4321", messages!.Single().Content);
        var list = await b.GetStringAsync("api/conversations");
        Assert.Contains("4321", list); // aperçu de la liste déchiffré

        // Modification : toujours chiffrée
        (await a.PutAsJsonAsync($"api/conversations/{conversation}/messages/{sent.Id}", new { content = "Code changé : 9876" })).EnsureSuccessStatusCode();
        Assert.DoesNotContain("9876", await StoredContentAsync(sent.Id));
    }

    [Fact]
    public async Task Reply_quotes_a_message_of_the_same_conversation()
    {
        var (a, b, conversation) = await ConversationAsync();
        var question = await SendAsync(a, conversation, "On se retrouve à quelle heure ?");
        var answer = await SendAsync(b, conversation, "À 18 h devant la gare", question.Id);
        Assert.Equal(question.Id, answer.ReplyTo!.Id);
        Assert.Equal("On se retrouve à quelle heure ?", answer.ReplyTo.Excerpt);

        var messages = await a.GetFromJsonAsync<List<Msg>>($"api/conversations/{conversation}/messages");
        Assert.Equal(question.Id, messages!.Single(m => m.Id == answer.Id).ReplyTo!.Id);

        // Message d'une autre conversation : introuvable
        var (_, _, other) = await ConversationAsync();
        Assert.Equal(HttpStatusCode.NotFound,
            (await a.PostAsJsonAsync($"api/conversations/{conversation}/messages", new { content = "Test", replyToId = other })).StatusCode);

        // Message cité supprimé : la citation l'indique, et on ne peut plus y répondre
        (await a.DeleteAsync($"api/conversations/{conversation}/messages/{question.Id}")).EnsureSuccessStatusCode();
        messages = await a.GetFromJsonAsync<List<Msg>>($"api/conversations/{conversation}/messages");
        var cited = messages!.Single(m => m.Id == answer.Id).ReplyTo!;
        Assert.True(cited.IsDeleted);
        Assert.Equal(string.Empty, cited.Excerpt);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await b.PostAsJsonAsync($"api/conversations/{conversation}/messages", new { content = "Encore ?", replyToId = question.Id })).StatusCode);
    }

    [Fact]
    public async Task Admin_job_encrypts_existing_plain_messages()
    {
        var (a, b, conversation) = await ConversationAsync();
        var sent = await SendAsync(a, conversation, "Ancien message en clair");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE messaging.messages SET \"Content\" = 'Ancien message en clair' WHERE \"Id\" = {sent.Id}");
        }

        // Lisible avant la reprise
        Assert.Equal("Ancien message en clair", (await b.GetFromJsonAsync<List<Msg>>($"api/conversations/{conversation}/messages"))!.Single().Content);

        Assert.Equal(HttpStatusCode.Forbidden, (await (await AdminAsync("moderation@beninconnect.test")).GetAsync("api/admin/messaging/encryption")).StatusCode);
        var admin = await AdminAsync("admin@beninconnect.test");
        Assert.True((await admin.GetFromJsonAsync<Status>("api/admin/messaging/encryption"))!.PlainMessages >= 1);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("api/admin/messaging/encryption/run", null)).StatusCode);
        Status? status = null;
        for (var i = 0; i < 50; i++, await Task.Delay(100))
        {
            status = await admin.GetFromJsonAsync<Status>("api/admin/messaging/encryption");
            if (!status!.Running)
            {
                break;
            }
        }

        Assert.False(status!.Running);
        Assert.Null(status.Error);
        Assert.Equal(0, status.PlainMessages);
        Assert.StartsWith("enc1:", await StoredContentAsync(sent.Id));
        Assert.Equal("Ancien message en clair", (await b.GetFromJsonAsync<List<Msg>>($"api/conversations/{conversation}/messages"))!.Single().Content);
    }

    [Fact]
    public void Cipher_reads_previous_keys_and_rejects_tampering()
    {
        var oldKey = RandomNumberGenerator.GetBytes(32);
        var newKey = RandomNumberGenerator.GetBytes(32);
        var old = new MessageCipher(oldKey);
        var stored = old.Protect("Bonjour 👋");
        Assert.NotEqual(stored, old.Protect("Bonjour 👋")); // nonce aléatoire
        Assert.Equal("Bonjour 👋", old.Reveal(stored));
        Assert.Equal("texte en clair", old.Reveal("texte en clair"));
        Assert.Equal(string.Empty, old.Protect(string.Empty));

        Assert.Equal(MessageCipher.Unreadable, new MessageCipher(newKey).Reveal(stored)); // clé perdue
        Assert.Equal("Bonjour 👋", new MessageCipher(newKey, [oldKey]).Reveal(stored)); // rotation
        var tampered = stored[..^4] + (stored[^4] == 'A' ? "B" : "A") + stored[^3..];
        Assert.Equal(MessageCipher.Unreadable, old.Reveal(tampered));
        Assert.False(MessageCipher.IsValidKey("trop-courte"));
        Assert.True(MessageCipher.IsValidKey(Convert.ToBase64String(newKey)));
    }
}
