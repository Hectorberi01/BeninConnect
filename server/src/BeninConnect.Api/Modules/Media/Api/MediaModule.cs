using BeninConnect.Modules.Media.Application;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Modules.Media.Domain;
using BeninConnect.Modules.Media.Infrastructure;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

namespace BeninConnect.Modules.Media.Api;

/// <summary>Photos (profil, couverture, publications) : disque local ou S3, servies sous /uploads.</summary>
public sealed class MediaModule : IModule, IModulePipeline
{
    private const string CacheControl = "public, max-age=31536000, immutable";

    public string Name => "Media";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleHandlers("BeninConnect.Modules.Media");
        services.AddScoped<IMediaModule, MediaModuleService>();

        var media = builder.Configuration.GetSection(MediaSettings.Section).Get<MediaSettings>() ?? new MediaSettings();
        services.AddSingleton(media);
        if (media.IsS3)
        {
            services.AddSingleton<IMediaStorage>(S3MediaStorage.Create(media.S3));
            services.AddSingleton(new AttachmentStore(S3MediaStorage.CreatePrivate(media.S3)));
            return;
        }

        // media/uploads : photos publiques (/uploads) ; media/private : pièces jointes, jamais servies directement.
        var root = media.Root is { Length: > 0 } configured ? configured : Path.Combine(builder.Environment.ContentRootPath, "media");
        var uploads = Path.Combine(root, "uploads");
        var attachments = Path.Combine(root, "private");
        Directory.CreateDirectory(uploads);
        Directory.CreateDirectory(attachments);
        services.AddSingleton<IMediaStorage>(new LocalMediaStorage(uploads));
        services.AddSingleton(new AttachmentStore(new LocalMediaStorage(attachments)));
    }

    public void UsePipeline(WebApplication app)
    {
        if (app.Services.GetRequiredService<IMediaStorage>() is LocalMediaStorage local)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(local.Directory),
                RequestPath = "/uploads",
                OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = CacheControl,
            });
        }
    }

    /// <summary>« bytes=début-fin » ou « bytes=début- » (une seule plage) ; null sinon.</summary>
    internal static (long From, long? To)? ParseRange(string header)
    {
        if (!header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) || header.Contains(','))
        {
            return null;
        }

        var parts = header[6..].Split('-', 2);
        if (parts.Length != 2 || !long.TryParse(parts[0], out var from) || from < 0)
        {
            return null;
        }

        if (parts[1].Length == 0)
        {
            return (from, null);
        }

        return long.TryParse(parts[1], out var to) && to >= from ? (from, to) : null;
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/media", async (IFormFile? file, ISender s, CancellationToken ct) =>
            {
                await using var content = file?.OpenReadStream() ?? Stream.Null;
                return Results.Ok(await s.Send(new StoreImageCommand(content, file?.Length ?? 0), ct));
            })
            .WithTags("Médias")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimits.Uploads)
            .DisableAntiforgery();

        app.MapPost("/api/media/video", async (IFormFile? file, ISender s, CancellationToken ct) =>
            {
                await using var content = file?.OpenReadStream() ?? Stream.Null;
                return Results.Ok(await s.Send(new StoreVideoCommand(content, file?.Length ?? 0), ct));
            })
            .WithTags("Médias")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimits.Uploads)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(VideoFile.MaxBytes + 1024 * 1024));

        if (app.ServiceProvider.GetRequiredService<IMediaStorage>() is LocalMediaStorage)
        {
            return; // fichiers statiques : plages (Range) gérées nativement
        }

        // S3 : redirection vers l'URL publique, sinon l'API relaie le fichier (avec plages Range pour la vidéo).
        app.MapGet("/uploads/{name}", async (string name, IMediaStorage storage, MediaSettings settings, HttpContext http, CancellationToken ct) =>
            {
                if (!ImageFile.IsValidName(name))
                {
                    return Results.NotFound();
                }

                if (string.IsNullOrWhiteSpace(settings.S3.PublicBaseUrl) && ParseRange(http.Request.Headers.Range.ToString()) is { } range)
                {
                    if (await storage.OpenRangeAsync(name, range.From, range.To, ct) is not { } part)
                    {
                        return Results.StatusCode(StatusCodes.Status416RangeNotSatisfiable);
                    }

                    await using (part.Content)
                    {
                        http.Response.StatusCode = StatusCodes.Status206PartialContent;
                        http.Response.ContentType = part.ContentType;
                        http.Response.ContentLength = part.To - part.From + 1;
                        http.Response.Headers.ContentRange = $"bytes {part.From}-{part.To}/{part.Total}";
                        http.Response.Headers.AcceptRanges = "bytes";
                        http.Response.Headers.CacheControl = CacheControl;
                        await part.Content.CopyToAsync(http.Response.Body, ct);
                    }

                    return Results.Empty;
                }

                if (await storage.OpenAsync(name, ct) is not { } image)
                {
                    return Results.NotFound();
                }

                if (image.RedirectUrl is { } url)
                {
                    return Results.Redirect(url, permanent: true);
                }

                http.Response.Headers.CacheControl = CacheControl;
                http.Response.Headers.AcceptRanges = "bytes";
                http.Response.ContentLength = image.Length;
                return Results.Stream(image.Content!, image.ContentType);
            })
            .ExcludeFromDescription();
    }
}
