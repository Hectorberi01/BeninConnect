using BeninConnect.Modules.Media.Application;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Modules.Media.Domain;
using BeninConnect.Modules.Media.Infrastructure;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;
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
            return;
        }

        var root = media.Root is { Length: > 0 } configured ? configured : Path.Combine(builder.Environment.ContentRootPath, "media");
        var uploads = Path.Combine(root, "uploads");
        Directory.CreateDirectory(uploads);
        services.AddSingleton<IMediaStorage>(new LocalMediaStorage(uploads));
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

        if (app.ServiceProvider.GetRequiredService<IMediaStorage>() is LocalMediaStorage)
        {
            return;
        }

        // S3 : redirection vers l'URL publique, sinon l'API relaie le fichier.
        app.MapGet("/uploads/{name}", async (string name, IMediaStorage storage, HttpContext http, CancellationToken ct) =>
            {
                if (!ImageFile.IsValidName(name) || await storage.OpenAsync(name, ct) is not { } image)
                {
                    return Results.NotFound();
                }

                if (image.RedirectUrl is { } url)
                {
                    return Results.Redirect(url, permanent: true);
                }

                http.Response.Headers.CacheControl = CacheControl;
                http.Response.ContentLength = image.Length;
                return Results.Stream(image.Content!, image.ContentType);
            })
            .ExcludeFromDescription();
    }
}
