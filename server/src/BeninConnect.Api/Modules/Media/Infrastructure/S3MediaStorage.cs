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
    private readonly string _prefix;
    private readonly bool _public;
    private readonly AmazonS3Client _client;
    private readonly bool _https;
    private Lazy<Task> _bucketReady;

    /// <param name="prefix">« uploads/ » (photos publiques) ou « private/ » (pièces jointes des discussions, jamais redirigées).</param>
    public S3MediaStorage(S3Settings s3, string prefix = ImageFile.PathPrefix, bool isPublic = true)
    {
        _s3 = s3;
        _prefix = prefix;
        _public = isPublic;
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

    private string Key(string name) => _prefix + name;

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
        if (_public)
        {
            request.Headers.CacheControl = CacheControl;
        }

        await _client.PutObjectAsync(request, ct);
    }

    public async Task<StoredImage?> OpenAsync(string name, CancellationToken ct)
    {
        if (_public && !string.IsNullOrWhiteSpace(_s3.PublicBaseUrl))
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

    public async Task<StoredRange?> OpenRangeAsync(string name, long from, long? to, CancellationToken ct)
    {
        try
        {
            var obj = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _s3.Bucket,
                Key = Key(name),
                // Fin inconnue : borne haute, S3 s'arrête à la fin du fichier (RFC 9110)
                ByteRange = new ByteRange(from, to ?? long.MaxValue - 1),
            }, ct);

            // Content-Range : « bytes 0-1023/52428800 »
            var range = obj.ContentRange ?? string.Empty;
            var slash = range.LastIndexOf('/');
            var total = slash > 0 && long.TryParse(range[(slash + 1)..], out var t) ? t : Convert.ToInt64((object?)obj.ContentLength);
            var last = from + Convert.ToInt64((object?)obj.ContentLength) - 1;
            return new StoredRange(obj.ResponseStream, from, last, total, ImageFile.ContentType(name));
        }
        catch (AmazonS3Exception e) when (e.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            return null;
        }
    }

    public Task DeleteAsync(string name, CancellationToken ct) => _client.DeleteObjectAsync(_s3.Bucket, Key(name), ct);

    public async Task<(long Files, long Bytes)> UsageAsync(CancellationToken ct)
    {
        var (count, bytes) = (0L, 0L);
        var request = new ListObjectsV2Request { BucketName = _s3.Bucket, Prefix = _prefix };
        await foreach (var obj in _client.Paginators.ListObjectsV2(request).S3Objects.WithCancellation(ct))
        {
            count++;
            bytes += Convert.ToInt64((object?)obj.Size); // long? en SDK v4
        }

        return (count, bytes);
    }

    public void Dispose() => _client.Dispose();

    public static IMediaStorage Create(S3Settings settings) => new S3MediaStorage(settings);

    /// <summary>Pièces jointes privées (préfixe private/) : toujours relayées par l'API après contrôle d'accès.</summary>
    public static IMediaStorage CreatePrivate(S3Settings settings) =>
        new S3MediaStorage(string.IsNullOrWhiteSpace(settings.PrivateBucket) ? settings : settings.WithBucket(settings.PrivateBucket), "private/", isPublic: false);
}
