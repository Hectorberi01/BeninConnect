using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Modules.Media.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Media.Application;

/// <summary>Stockage privé des pièces jointes (disque media/private ou préfixe S3 private/) : jamais servi directement.</summary>
public sealed class AttachmentStore(IMediaStorage storage)
{
    public async Task<StoredAttachment> SaveAsync(Stream content, long length, CancellationToken ct)
    {
        if (length == 0)
        {
            throw new DomainException("Aucun fichier reçu.");
        }

        if (length > AttachmentFile.MaxBytes)
        {
            throw new DomainException("Le fichier ne doit pas dépasser 20 Mo.");
        }

        var header = new byte[12];
        await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        content.Position = 0;

        var (type, extension) = AttachmentFile.Detect(header)
                                ?? throw new DomainException("Format non pris en charge : envoyez une photo, un PDF ou un fichier audio.");
        var name = ImageFile.NewName(extension);
        var contentType = AttachmentFile.ContentType(name);
        await storage.SaveAsync(name, content, contentType, ct);
        return new StoredAttachment(name, type.ToString(), contentType, length);
    }

    public async Task<AttachmentContent?> OpenAsync(string name, CancellationToken ct)
    {
        if (!AttachmentFile.IsValidName(name) || await storage.OpenAsync(name, ct) is not { Content: { } content } file)
        {
            return null;
        }

        // Flux S3 non positionnable : copié en mémoire (20 Mo max) pour servir les plages Range,
        // EXIGÉES par le lecteur audio iOS.
        if (!content.CanSeek)
        {
            var buffer = new MemoryStream();
            await using (content)
            {
                await content.CopyToAsync(buffer, ct);
            }

            buffer.Position = 0;
            return new AttachmentContent(buffer, buffer.Length, AttachmentFile.ContentType(name));
        }

        return new AttachmentContent(content, file.Length, AttachmentFile.ContentType(name));
    }

    public Task DeleteAsync(string name, CancellationToken ct) =>
        AttachmentFile.IsValidName(name) ? storage.DeleteAsync(name, ct) : Task.CompletedTask;
}

public sealed class DeleteReleasedAttachments(AttachmentStore store) : IIntegrationEventHandler<AttachmentsReleased>
{
    public async Task Handle(AttachmentsReleased e, CancellationToken ct)
    {
        foreach (var name in e.Names.Distinct())
        {
            await store.DeleteAsync(name, ct);
        }
    }
}
