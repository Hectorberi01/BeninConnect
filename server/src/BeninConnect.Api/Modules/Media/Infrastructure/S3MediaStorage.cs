using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using BeninConnect.Modules.Media.Application;
using BeninConnect.Modules.Media.Domain;

namespace BeninConnect.Modules.Media.Infrastructure;

/// <summary>Compatible AWS S3, Cloudflare R2, Hetzner Object Storage, Scaleway, OVHcloud, MinIO.</summary>
public sealed class S3MediaStorage : IMediaStorage, IDisposable
{
    private const string CacheControl = "public, max-age=31536000, immutable"; // noms uniques : jamais modifiés
    private readonly S3Settings _s3;
    private readonly AmazonS3Client _client;
    private readonly bool _https;
    private Lazy<Task> _bucketReady;

    public S3MediaStorage(S3Settings s3)
    {
        _s3 = s3;
        var config = new AmazonS3Config
        {
            ForcePathStyle = s3.ForcePathStyle,
            // R2, Hetzner, MinIO : sommes de contrôle par défaut du SDK v4 non prises en charge partout.
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
        _bucketReady = new Lazy<Task>(() => s3.CreateBucket ? EnsureBucketAsync() : Task.CompletedTask);
    }

    // Au premier envoi (pas au démarrage : MinIO peut démarrer après l'API).
    private async Task EnsureBucketAsync()
    {
        try
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _s3.Bucket, UseClientRegion = true });
        }
        catch (AmazonS3Exception e) when (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }
    }

    private static string Key(string name) => ImageFile.PathPrefix + name;

    public async Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct)
    {
        try
        {
            await _bucketReady.Value;
        }
        catch
        {
            _bucketReady = new Lazy<Task>(EnsureBucketAsync); // échec réseau : nouvel essai au prochain envoi
            throw;
        }

        var request = new PutObjectRequest
        {
            BucketName = _s3.Bucket,
            Key = Key(name),
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            UseChunkEncoding = false,
            DisablePayloadSigning = _https, // HTTPS uniquement
        };
        request.Headers.CacheControl = CacheControl;
        await _client.PutObjectAsync(request, ct);
    }

    public async Task<StoredImage?> OpenAsync(string name, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_s3.PublicBaseUrl))
        {
            return new StoredImage(null, null, ImageFile.ContentType(name), $"{_s3.PublicBaseUrl.TrimEnd('/')}/{Key(name)}");
        }

        try
        {
            var obj = await _client.GetObjectAsync(_s3.Bucket, Key(name), ct);
            return new StoredImage(obj.ResponseStream, obj.ContentLength, ImageFile.ContentType(name), null);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<(long Files, long Bytes)> UsageAsync(CancellationToken ct)
    {
        var (count, bytes) = (0L, 0L);
        var request = new ListObjectsV2Request { BucketName = _s3.Bucket, Prefix = "uploads/" };
        await foreach (var obj in _client.Paginators.ListObjectsV2(request).S3Objects.WithCancellation(ct))
        {
            count++;
            bytes += Convert.ToInt64((object?)obj.Size); // long? en SDK v4
        }

        return (count, bytes);
    }

    public void Dispose() => _client.Dispose();

    public static IMediaStorage Create(S3Settings settings) => new S3MediaStorage(settings);
}
