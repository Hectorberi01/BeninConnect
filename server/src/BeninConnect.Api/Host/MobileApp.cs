namespace BeninConnect.Host;

/// <summary>Version minimale de l'application mobile : en dessous, l'app affiche « Mettez à jour BéninConnect ».</summary>
public static class MobileApp
{
    public const string MinimumVersionKey = "MobileApp:MinimumVersion";

    public static void MapMobileApp(this WebApplication app)
    {
        app.MapGet("/api/app/version", (IConfiguration config, HttpContext ctx) =>
        {
            var minimum = config[MinimumVersionKey]?.Trim();
            ctx.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Ok(new { minimumVersion = Version.TryParse(minimum, out _) ? minimum : null });
        }).AllowAnonymous().WithTags("Application");
    }
}
