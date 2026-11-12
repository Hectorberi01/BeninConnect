using BeninConnect.Modules.Media.Application;
using BeninConnect.Modules.Media.Contracts;

namespace BeninConnect.Modules.Media.Infrastructure;

public sealed class MediaModuleService(IMediaStorage storage, AttachmentStore attachments, MediaSettings settings) : IMediaModule
{
    public async Task<MediaUsage> UsageAsync(CancellationToken ct)
    {
        var (files, bytes) = await storage.UsageAsync(ct);
        return new MediaUsage(files, bytes);
    }

    public Task<string> SaveImageAsync(Stream content, long length, CancellationToken ct) =>
        ImageUpload.SaveAsync(storage, content, length, ct);

    public Task<StoredAttachment> SaveAttachmentAsync(Stream content, long length, CancellationToken ct) =>
        attachments.SaveAsync(content, length, ct);

    public Task<AttachmentContent?> OpenAttachmentAsync(string name, CancellationToken ct) => attachments.OpenAsync(name, ct);

    public string? PublicUrl(string path) =>
        settings.IsS3 && !string.IsNullOrWhiteSpace(settings.S3.PublicBaseUrl) && path.StartsWith("uploads/", StringComparison.Ordinal)
            ? $"{settings.S3.PublicBaseUrl.TrimEnd('/')}/{path}"
            : null;
}
