using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Media.Contracts;

public sealed record MediaUsage(long Files, long Bytes);

/// <summary>Image, Audio ou File (PDF).</summary>
public sealed record StoredAttachment(string Name, string Kind, string ContentType, long Size);

public sealed record AttachmentContent(Stream Content, long? Length, string ContentType);

public interface IMediaModule
{
    /// <summary>Nombre de photos envoyées et volume total (page Système de la console).</summary>
    Task<MediaUsage> UsageAsync(CancellationToken ct);

    /// <summary>
    /// Enregistre une pièce jointe privée (photo, PDF, audio ; 20 Mo) après contrôle du format.
    /// Le contenu doit permettre la lecture répétée (Seek). Lève une DomainException si le fichier est refusé.
    /// </summary>
    Task<StoredAttachment> SaveAttachmentAsync(Stream content, long length, CancellationToken ct);

    /// <summary>null : fichier absent. L'appelant vérifie au préalable le droit d'accès.</summary>
    Task<AttachmentContent?> OpenAttachmentAsync(string name, CancellationToken ct);

    /// <summary>
    /// Enregistre une photo publique (JPEG, PNG, WebP, HEIC ; 10 Mo) et renvoie son chemin (uploads/…).
    /// Le contenu doit permettre la relecture (Seek). Lève une DomainException si le fichier est refusé.
    /// </summary>
    Task<string> SaveImageAsync(Stream content, long length, CancellationToken ct);

    /// <summary>URL publique directe d'une photo (domaine du bucket) ; null si les fichiers passent par l'API.</summary>
    string? PublicUrl(string path);
}

/// <summary>Photos qui ne sont plus utilisées (publication ou compte supprimé) : effacées du stockage.</summary>
public sealed record ImagesReleased(IReadOnlyList<string> Paths) : IIntegrationEvent;

/// <summary>Pièces jointes privées qui ne sont plus utilisées (message ou conversation supprimés).</summary>
public sealed record AttachmentsReleased(IReadOnlyList<string> Names) : IIntegrationEvent;
