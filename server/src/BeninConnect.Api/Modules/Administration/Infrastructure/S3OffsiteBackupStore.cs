using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using BeninConnect.Modules.Administration.Application;

namespace BeninConnect.Modules.Administration.Infrastructure;

/// <summary>Sauvegardes dans un bucket S3 compatible (Cloudflare R2, AWS, Hetzner, MinIO), sous le préfixe configuré.</summary>
public sealed class S3OffsiteBackupStore : IOffsiteBackupStore, IDisposable
{
    private readonly OffsiteSettings _s3;
    private readonly AmazonS3Client _client;
    private readonly bool _https;

    public S3OffsiteBackupStore(OffsiteSettings s3)
    {
        _s3 = s3;
        var config = new AmazonS3Config
        {
            ForcePathStyle = true,
            // R2 : sommes de contrôle par défaut du SDK v4 non prises en charge partout.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        if (string.IsNullOrWhiteSpace(s3.ServiceUrl))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(s3.Region);
        }
        else
        {
            config.ServiceURL = s3.ServiceUrl;
            config.AuthenticationRegion = s3.Region;
        }

        _https = config.ServiceURL?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) != true;
        _client = new AmazonS3Client(new BasicAWSCredentials(s3.AccessKey, s3.SecretKey), config);
    }

    private string Prefix => _s3.Prefix.Length == 0 || _s3.Prefix.EndsWith('/') ? _s3.Prefix : _s3.Prefix + "/";

    public async Task<IReadOnlyList<RemoteObject>> ListAsync(CancellationToken ct)
    {
        var result = new List<RemoteObject>();
        var request = new ListObjectsV2Request { BucketName = _s3.Bucket, Prefix = Prefix };
        await foreach (var obj in _client.Paginators.ListObjectsV2(request).S3Objects.WithCancellation(ct))
        {
            var modified = Convert.ToDateTime((object?)obj.LastModified); // DateTime? en SDK v4, parfois en heure locale
            var utc = modified.Kind == DateTimeKind.Local ? modified.ToUniversalTime() : DateTime.SpecifyKind(modified, DateTimeKind.Utc);
            result.Add(new RemoteObject(obj.Key[Prefix.Length..], Convert.ToInt64((object?)obj.Size), new DateTimeOffset(utc)));
        }

        return result;
    }

    public async Task UploadAsync(string key, string path, CancellationToken ct)
    {
        await using var content = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _s3.Bucket,
            Key = Prefix + key,
            InputStream = content,
            ContentType = "application/gzip",
            AutoCloseStream = false,
            UseChunkEncoding = false,
            DisablePayloadSigning = _https, // HTTPS uniquement
        }, ct);
    }

    public Task DeleteAsync(string key, CancellationToken ct) => _client.DeleteObjectAsync(_s3.Bucket, Prefix + key, ct);

    public async Task<Stream?> OpenAsync(string key, CancellationToken ct)
    {
        try
        {
            var obj = await _client.GetObjectAsync(_s3.Bucket, Prefix + key, ct);
            return obj.ResponseStream;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public void Dispose() => _client.Dispose();
}
