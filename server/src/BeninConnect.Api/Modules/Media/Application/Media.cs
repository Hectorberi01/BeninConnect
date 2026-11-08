using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Modules.Media.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Media.Application;

/// <summary>Photo stockée : contenu à relayer, ou URL publique vers laquelle rediriger.</summary>
public sealed record StoredImage(Stream? Content, long? Length, string ContentType, string? RedirectUrl);

/// <summary>Partie d'un fichier (requête HTTP Range, indispensable à la lecture vidéo sur iOS).</summary>
public sealed record StoredRange(Stream Content, long From, long To, long Total, string ContentType);

/// <summary>Disque local ou stockage objet S3.</summary>
public interface IMediaStorage
{
    Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct);

    /// <summary>null : photo absente.</summary>
    Task<StoredImage?> OpenAsync(string name, CancellationToken ct);

    Task<(long Files, long Bytes)> UsageAsync(CancellationToken ct);

    /// <summary>Sans effet si la photo n'existe pas.</summary>
    Task DeleteAsync(string name, CancellationToken ct);

    /// <summary>Octets From..To (To null : jusqu'à la fin) ; null si le fichier est absent ou la plage invalide.</summary>
    Task<StoredRange?> OpenRangeAsync(string name, long from, long? to, CancellationToken ct);
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

/// <summary>Efface les photos envoyées par les membres (uploads/...) ; les images du jeu de démonstration sont ignorées.</summary>
public sealed class DeleteReleasedImages(IMediaStorage storage) : IIntegrationEventHandler<ImagesReleased>
{
    public async Task Handle(ImagesReleased e, CancellationToken ct)
    {
        foreach (var path in e.Paths.Distinct())
        {
            if (!path.StartsWith(ImageFile.PathPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var name = path[ImageFile.PathPrefix.Length..];
            if (ImageFile.IsValidName(name))
            {
                await storage.DeleteAsync(name, ct);
            }
        }
    }
}

/// <summary>Vidéo d'une publication (60 Mo). Content doit permettre la lecture répétée (Seek).</summary>
public sealed record StoreVideoCommand(Stream Content, long Length) : ICommand<MediaResponse>;

public sealed class StoreVideoHandler(IMediaStorage storage) : IRequestHandler<StoreVideoCommand, MediaResponse>
{
    public async Task<MediaResponse> Handle(StoreVideoCommand request, CancellationToken ct)
    {
        if (request.Length == 0)
        {
            throw new DomainException("Aucune vidéo reçue.");
        }

        if (request.Length > VideoFile.MaxBytes)
        {
            throw new DomainException("La vidéo ne doit pas dépasser 60 Mo.");
        }

        var header = new byte[12];
        await request.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        request.Content.Position = 0;

        var extension = VideoFile.DetectExtension(header) ?? throw new DomainException("Format non pris en charge : envoyez une vidéo MP4 ou MOV.");
        var name = ImageFile.NewName(extension);
        await storage.SaveAsync(name, request.Content, ImageFile.ContentType(name), ct);
        return new MediaResponse(ImageFile.PathPrefix + name);
    }
}
