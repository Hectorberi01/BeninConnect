using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.Minio;

namespace BeninConnect.Api.Tests;

/// <summary>API avec stockage MinIO éphémère (comme le docker-compose) au lieu du disque.</summary>
public sealed class S3ApiFactory : ApiFactory
{
    public const string Bucket = "beninconnect-tests";
    // minio/minio retiré de Docker Hub (sept. 2026) : image Chainguard par défaut, remplaçable par BC_TEST_MINIO_IMAGE.
    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage(Environment.GetEnvironmentVariable("BC_TEST_MINIO_IMAGE") ?? "cgr.dev/chainguard/minio:latest")
        .WithCreateParameterModifier(p => p.User = "0") // l'image tourne en UID 65532, sans droit d'écriture sur /data
        .Build();

    protected override Task StartDependenciesAsync() => _minio.StartAsync();

    protected override void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("Media:Provider", "s3");
        builder.UseSetting("Media:S3:ServiceUrl", _minio.GetConnectionString());
        builder.UseSetting("Media:S3:Region", "us-east-1");
        builder.UseSetting("Media:S3:Bucket", Bucket);
        builder.UseSetting("Media:S3:AccessKey", _minio.GetAccessKey());
        builder.UseSetting("Media:S3:SecretKey", _minio.GetSecretKey());
        builder.UseSetting("Media:S3:CreateBucket", "true"); // bucket créé par l'API au premier envoi
    }

    protected override Task StopDependenciesAsync() => _minio.DisposeAsync().AsTask();

    public AmazonS3Client Client() => new(new BasicAWSCredentials(_minio.GetAccessKey(), _minio.GetSecretKey()),
        new AmazonS3Config { ServiceURL = _minio.GetConnectionString(), ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
}

public sealed class S3MediaTests(S3ApiFactory factory) : IClassFixture<S3ApiFactory>
{
    private sealed record TokenView(string Token);

    private sealed record MediaView(string Path);

    [Fact]
    public async Task Photo_is_stored_in_the_bucket_and_served_under_uploads()
    {
        var http = factory.CreateClient();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenView>())!.Token);

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 1, 2, 3, 4];
        var form = new MultipartFormDataContent { { new ByteArrayContent(png), "file", "photo.png" } };
        var media = (await (await http.PostAsync("api/media", form)).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<MediaView>())!;
        Assert.Matches("^uploads/[a-f0-9]{32}\\.png$", media.Path);

        using var s3 = factory.Client();
        var stored = await s3.GetObjectMetadataAsync(S3ApiFactory.Bucket, media.Path);
        Assert.Equal("image/png", stored.Headers.ContentType);

        var served = await http.GetAsync(media.Path);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Contains("immutable", served.Headers.CacheControl?.ToString());
        Assert.Equal(png, await served.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"uploads/{Guid.NewGuid():N}.png")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("uploads/..%2Fappsettings.json")).StatusCode);
    }

    [Fact]
    public async Task Videos_are_relayed_with_byte_ranges_and_attachments_stay_in_the_private_prefix()
    {
        var http = factory.CreateClient();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenView>())!.Token);

        var form = new MultipartFormDataContent { { new ByteArrayContent(VideoTests.Mp4), "file", "clip.mp4" } };
        var video = (await (await http.PostAsync("api/media/video", form)).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<MediaView>())!;

        var request = new HttpRequestMessage(HttpMethod.Get, video.Path);
        request.Headers.Range = new RangeHeaderValue(4, 11);
        var part = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, part.StatusCode);
        Assert.Equal($"bytes 4-11/{VideoTests.Mp4.Length}", part.Content.Headers.ContentRange?.ToString());
        Assert.Equal(VideoTests.Mp4[4..12], await part.Content.ReadAsByteArrayAsync());

        var open = new HttpRequestMessage(HttpMethod.Get, video.Path);
        open.Headers.Range = new RangeHeaderValue(100, null);
        var tail = await http.SendAsync(open);
        Assert.Equal(HttpStatusCode.PartialContent, tail.StatusCode);
        Assert.Equal(VideoTests.Mp4[100..], await tail.Content.ReadAsByteArrayAsync());

        // Pièce jointe d'une discussion : préfixe private/, jamais servie sous /uploads
        var conversations = await http.GetFromJsonAsync<List<ConversationView>>("api/conversations");
        var pdf = "%PDF-1.4 test S3"u8.ToArray();
        var attachment = new MultipartFormDataContent { { new ByteArrayContent(pdf), "file", "devis.pdf" } };
        var message = (await (await http.PostAsync($"api/conversations/{conversations![0].Conversation.Id}/attachments", attachment))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<IdView>())!;
        using var s3 = factory.Client();
        var keys = (await s3.ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request { BucketName = S3ApiFactory.Bucket, Prefix = "private/" })).S3Objects;
        Assert.Contains(keys ?? [], o => o.Key.EndsWith(".pdf", StringComparison.Ordinal));
        Assert.Equal(pdf, await (await http.GetAsync($"api/messages/{message.Id}/attachment")).Content.ReadAsByteArrayAsync());
    }

    private sealed record IdView(Guid Id);

    private sealed record ConversationRef(Guid Id);

    private sealed record ConversationView(ConversationRef Conversation);
}
