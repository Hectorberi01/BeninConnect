
using BeninConnect.Modules.Media.Application;
using BeninConnect.Modules.Media.Domain;

namespace BeninConnect.Modules.Media.Infrastructure;

/// <summary>Section « Media » : disque local (développement, VPS simple) ou stockage objet S3.</summary>
public sealed class MediaSettings
{
    public const string Section = "Media";

    /// <summary>local | s3</summary>
    public string Provider { get; set; } = "local";

    /// <summary>Dossier racine en mode local (photos dans Root/uploads).</summary>
    public string? Root { get; set; }

    public S3Settings S3 { get; set; } = new();

    public bool IsS3 => Provider.Equals("s3", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Compatible MinIO, AWS S3, Cloudflare R2, Hetzner Object Storage, Scaleway, OVHcloud, MinIO.</summary>
public sealed class S3Settings
{
    /// <summary>Point d'accès, ex. https://fsn1.your-objectstorage.com ; vide = AWS S3 (Region).</summary>
    public string? ServiceUrl { get; set; }

    public string Region { get; set; } = "auto";
    public string Bucket { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>URL publique du bucket ou du CDN (ex. https://media.beninconnect.app). Vide = l'API relaie les photos.</summary>
    public string? PublicBaseUrl { get; set; }

    public bool ForcePathStyle { get; set; } = true;

    /// <summary>Crée le bucket s'il n'existe pas (MinIO auto-hébergé). Inutile chez un fournisseur.</summary>
    public bool CreateBucket { get; set; }

    /// <summary>
    /// Bucket des pièces jointes des discussions (privé, jamais public). Vide : même bucket que les photos, préfixe private/.
    /// À renseigner si le bucket principal est public (PublicBaseUrl, domaine public R2).
    /// </summary>
    public string? PrivateBucket { get; set; }

    public S3Settings WithBucket(string bucket) => new()
    {
        ServiceUrl = ServiceUrl, Region = Region, Bucket = bucket, AccessKey = AccessKey, SecretKey = SecretKey,
        ForcePathStyle = ForcePathStyle, CreateBucket = CreateBucket,
    };
}

public sealed class LocalMediaStorage(string directory) : IMediaStorage
{
    public string Directory { get; } = directory;

    public async Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct)
    {
        await using var target = File.Create(Path.Combine(Directory, name));
        await content.CopyToAsync(target, ct);
    }

    // Mode local : en pratique servi par UseStaticFiles (plus rapide).
    public Task<StoredImage?> OpenAsync(string name, CancellationToken ct)
    {
        var path = Path.Combine(Directory, name);
        return Task.FromResult(File.Exists(path)
            ? new StoredImage(File.OpenRead(path), new FileInfo(path).Length, ImageFile.ContentType(name), null)
            : null);
    }

    public Task<StoredRange?> OpenRangeAsync(string name, long from, long? to, CancellationToken ct)
    {
        var path = Path.Combine(Directory, name);
        if (!File.Exists(path))
        {
            return Task.FromResult<StoredRange?>(null);
        }

        var total = new FileInfo(path).Length;
        var end = Math.Min(to ?? total - 1, total - 1);
        if (from < 0 || from > end)
        {
            return Task.FromResult<StoredRange?>(null);
        }

        var stream = File.OpenRead(path);
        stream.Seek(from, SeekOrigin.Begin);
        return Task.FromResult<StoredRange?>(new StoredRange(stream, from, end, total, ImageFile.ContentType(name)));
    }

    public Task DeleteAsync(string name, CancellationToken ct)
    {
        File.Delete(Path.Combine(Directory, name));
        return Task.CompletedTask;
    }

    public Task<(long Files, long Bytes)> UsageAsync(CancellationToken ct)
    {
        var files = new DirectoryInfo(Directory).EnumerateFiles().ToList();
        return Task.FromResult(((long)files.Count, files.Sum(f => f.Length)));
    }
}
