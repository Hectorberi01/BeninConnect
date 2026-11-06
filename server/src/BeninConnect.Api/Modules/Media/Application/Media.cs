using BeninConnect.Modules.Media.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Media.Application;

/// <summary>Photo stockée : contenu à relayer, ou URL publique vers laquelle rediriger.</summary>
public sealed record StoredImage(Stream? Content, long? Length, string ContentType, string? RedirectUrl);

/// <summary>Disque local ou stockage objet S3.</summary>
public interface IMediaStorage
{
    Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct);

    /// <summary>null : photo absente.</summary>
    Task<StoredImage?> OpenAsync(string name, CancellationToken ct);

    Task<(long Files, long Bytes)> UsageAsync(CancellationToken ct);
}

public sealed record MediaResponse(string Path);

/// <summary>Content doit permettre la lecture répétée (Seek) : la signature est lue avant l'enregistrement.</summary>
public sealed record StoreImageCommand(Stream Content, long Length) : ICommand<MediaResponse>;

public sealed class StoreImageHandler(IMediaStorage storage) : IRequestHandler<StoreImageCommand, MediaResponse>
{
    public async Task<MediaResponse> Handle(StoreImageCommand request, CancellationToken ct)
    {
        if (request.Length == 0)
        {
            throw new DomainException("Aucune image reçue.");
        }

        if (request.Length > ImageFile.MaxBytes)
        {
            throw new DomainException("L'image ne doit pas dépasser 10 Mo.");
        }

        var header = new byte[12];
        await request.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        request.Content.Position = 0;

        var extension = ImageFile.DetectExtension(header)
                        ?? throw new DomainException("Format non pris en charge : utilisez une photo JPEG, PNG, WebP ou HEIC.");
        var name = ImageFile.NewName(extension);
        await storage.SaveAsync(name, request.Content, ImageFile.ContentType(name), ct);
        return new MediaResponse(ImageFile.PathPrefix + name);
    }
}
