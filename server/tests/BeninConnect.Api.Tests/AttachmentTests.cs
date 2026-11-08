using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace BeninConnect.Api.Tests;

/// <summary>Pièces jointes des discussions : formats acceptés, accès réservé aux participants, suppression.</summary>
[Collection(ApiCollection.Name)]
public sealed class AttachmentTests(ApiFactory factory)
{
    private sealed record TokenView(string Token);

    private sealed record IdView(Guid Id);

    private sealed record AttachmentView(string Kind, string FileName, string ContentType, long Size, int? DurationMs);

    private sealed record MessageView(Guid Id, string Content, bool IsDeleted, AttachmentView? Attachment);

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% BeninConnect test\n1 0 obj << >> endobj\n%%EOF\n");

    private static readonly byte[] M4a = [0, 0, 0, 0x20, .. "ftypM4A "u8, 0, 0, 0, 0, .. "M4A mp42isom"u8, 0, 0, 0, 8, .. "free"u8];

    private async Task<(HttpClient Http, Guid Id, string Token)> MemberAsync()
    {
        var http = factory.CreateClient();
        var email = $"pj.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = "Ayaba", lastName = "Fichier", email, country = "Bénin", city = "Cotonou", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Secret123!" });
        var token = (await login.Content.ReadFromJsonAsync<TokenView>())!.Token;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (http, (await http.GetFromJsonAsync<IdView>("api/auth/me"))!.Id, token);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient http, Guid conversation, byte[] bytes, string fileName, string? caption = null, int? durationMs = null)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", fileName } };
        if (caption is not null)
        {
            form.Add(new StringContent(caption), "content");
        }

        if (durationMs is not null)
        {
            form.Add(new StringContent(durationMs.ToString()!), "durationMs");
        }

        return http.PostAsync($"api/conversations/{conversation}/attachments", form);
    }

    [Fact]
    public async Task Attachments_are_private_to_the_conversation_and_removed_with_their_message()
    {
        var (alice, _, _) = await MemberAsync();
        var (bob, bobId, bobToken) = await MemberAsync();
        var (carol, _, _) = await MemberAsync();
        var conversation = (await (await alice.PostAsJsonAsync("api/conversations/direct", new { otherUserId = bobId }))
            .Content.ReadFromJsonAsync<IdView>())!.Id;

        // PDF avec légende
        var sent = await UploadAsync(alice, conversation, Pdf, "../../Devis mariage.pdf", "Voici le devis");
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        var message = (await sent.Content.ReadFromJsonAsync<MessageView>())!;
        Assert.Equal("Voici le devis", message.Content);
        Assert.Equal(new AttachmentView("File", "Devis mariage.pdf", "application/pdf", Pdf.Length, null), message.Attachment);

        var url = $"api/messages/{message.Id}/attachment";
        var download = await bob.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Pdf, await download.Content.ReadAsByteArrayAsync());

        // Jeton dans l'URL (image, lecteur audio, PDF ouverts par le téléphone)
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync($"{url}?access_token={bobToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await carol.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await carol.PostAsync($"api/conversations/{conversation}/attachments",
            new MultipartFormDataContent { { new ByteArrayContent(Pdf), "file", "x.pdf" } })).StatusCode);

        // Message vocal : durée conservée
        var voice = (await (await UploadAsync(bob, conversation, M4a, "enregistrement.m4a", durationMs: 4200)).Content.ReadFromJsonAsync<MessageView>())!;
        Assert.Equal(("Audio", "audio/mp4", 4200), (voice.Attachment!.Kind, voice.Attachment.ContentType, voice.Attachment.DurationMs));

        // Formats refusés
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(alice, conversation, [0x4D, 0x5A, .. new byte[30]], "virus.exe")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(alice, conversation, [], "vide.pdf")).StatusCode);

        // Suppression : fichier effacé
        var deleted = (await (await alice.DeleteAsync($"api/conversations/{conversation}/messages/{message.Id}")).Content.ReadFromJsonAsync<MessageView>())!;
        Assert.True(deleted.IsDeleted);
        Assert.Null(deleted.Attachment);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(url)).StatusCode);
    }
}
