using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BeninConnect.Api.Tests;

/// <summary>Vidéos des publications : format, lecture partielle (Range), suppression avec la publication.</summary>
[Collection(ApiCollection.Name)]
public sealed class VideoTests(ApiFactory factory)
{
    private sealed record TokenView(string Token);

    private sealed record MediaView(string Path);

    private sealed record PostView(Guid Id, string? ImagePath, string? VideoPath);

    /// <summary>En-tête MP4 (ftyp isom) suivi de données.</summary>
    public static readonly byte[] Mp4 = [0, 0, 0, 0x18, .. "ftypisom"u8, 0, 0, 2, 0, .. "isomiso2"u8, .. Enumerable.Range(0, 200).Select(i => (byte)i)];

    private async Task<HttpClient> SignInAsync()
    {
        var http = factory.CreateClient();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenView>())!.Token);
        return http;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient http, string endpoint, byte[] bytes, string name) =>
        http.PostAsync(endpoint, new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", name } });

    [Fact]
    public async Task A_post_can_carry_a_video_served_with_byte_ranges()
    {
        var http = await SignInAsync();
        var upload = await UploadAsync(http, "api/media/video", Mp4, "clip.mp4");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var video = (await upload.Content.ReadFromJsonAsync<MediaView>())!.Path;
        Assert.Matches("^uploads/[a-f0-9]{32}\\.mp4$", video);

        // Lecture partielle (exigée par le lecteur vidéo d'iOS)
        var request = new HttpRequestMessage(HttpMethod.Get, video);
        request.Headers.Range = new RangeHeaderValue(0, 3);
        var part = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, part.StatusCode);
        Assert.Equal(Mp4[..4], await part.Content.ReadAsByteArrayAsync());
        Assert.Equal("video/mp4", part.Content.Headers.ContentType?.MediaType);

        // Une photo n'est pas une vidéo, et inversement
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(http, "api/media/video", [0xFF, 0xD8, 0xFF, .. new byte[20]], "photo.jpg")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("api/posts",
            new { content = "Mauvais chemin", visibility = "Public", videoPath = "uploads/../secret.mp4" })).StatusCode);

        var created = await http.PostAsJsonAsync("api/posts", new { content = "Ma première vidéo", visibility = "Public", videoPath = video });
        var post = (await created.Content.ReadFromJsonAsync<PostView>())!;
        Assert.Equal(video, post.VideoPath);

        // Suppression : la vidéo est effacée du stockage
        Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync($"api/posts/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(video)).StatusCode);
    }
}
