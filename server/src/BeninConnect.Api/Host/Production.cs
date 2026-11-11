using System.Threading.RateLimiting;
using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Identity.Infrastructure;
using BeninConnect.Modules.Media.Infrastructure;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace BeninConnect.Host;

/// <summary>Réglages de production : garde-fous au démarrage, proxy, limites de débit, CORS, en-têtes.</summary>
public static class Production
{
    public const string DevSigningKey = "dev-only-signing-key-beninconnect-please-change-0123456789";

    /// <summary>Hors développement, refuse de démarrer avec une configuration dangereuse.</summary>
    public static void Validate(IConfiguration config, IHostEnvironment env, JwtOptions jwt, AuthSettings auth, EmailSettings email)
    {
        if (env.IsDevelopment())
        {
            return;
        }

        // Revue Apple / Google : protections de production, avec code fixe, données de démo et emails journalisés
        var review = env.IsReview();
        var errors = new List<string>();
        if (jwt.SigningKey == DevSigningKey)
        {
            errors.Add("Jwt__SigningKey : la clé de développement est interdite (générez-en une : openssl rand -base64 48).");
        }

        if (auth.FixedVerificationCode is not null && !review)
        {
            errors.Add("Auth__FixedVerificationCode doit être vide : un code fixe permet de valider n'importe quel email.");
        }

        if (!email.IsSmtp && !email.IsResend && !config.GetValue("Email:AllowLogProvider", false) && !review)
        {
            errors.Add("Email__Provider=resend (Email__ResendApiKey, Email__From) ou smtp requis : sinon aucun code n'est envoyé.");
        }

        if (email.IsResend && (string.IsNullOrWhiteSpace(email.ResendApiKey) || string.IsNullOrWhiteSpace(email.From)))
        {
            errors.Add("Email__ResendApiKey et Email__From sont obligatoires avec le fournisseur resend.");
        }

        if (email.IsSmtp && (string.IsNullOrWhiteSpace(email.Host) || string.IsNullOrWhiteSpace(email.From)))
        {
            errors.Add("Email__Host et Email__From sont obligatoires avec le fournisseur smtp.");
        }

        if (config.GetValue("Database:SeedDemoData", true) && !review)
        {
            errors.Add("Database__SeedDemoData doit valoir false (comptes de démonstration aux mots de passe publics).");
        }

        var reviewSettings = config.GetSection(ReviewSettings.Section).Get<ReviewSettings>() ?? new ReviewSettings();
        if (review && (string.IsNullOrWhiteSpace(reviewSettings.AccountEmail) || (reviewSettings.AccountPassword?.Length ?? 0) < 12
                       || reviewSettings.AccountPassword == DemoData.DemoDataSeeder.DemoPassword))
        {
            errors.Add("Review__AccountEmail et Review__AccountPassword (12 caractères au moins, pas celui de la démo publique) sont obligatoires en mode Review.");
        }

        if (!review && reviewSettings.ActiveEmails.Length > 0 && !Uri.TryCreate(reviewSettings.ApiUrl, UriKind.Absolute, out _))
        {
            errors.Add("Review__ApiUrl doit être l'adresse https de l'API de revue (ex. https://review-api.monbenin.com).");
        }

        var db = new NpgsqlConnectionStringBuilder(config.GetConnectionString("Database"));
        var socket = db.Host?.StartsWith('/') == true; // Cloud SQL via socket Unix : chiffré par le connecteur
        if (!socket && db.SslMode is not (SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull) && !config.GetValue("Database:AllowInsecure", false))
        {
            errors.Add("ConnectionStrings__Database : base distante, chiffrement obligatoire (SSL Mode=Require ou VerifyFull).");
        }

        // Database:AllowLocalhost : tests d'intégration seulement
        if (db.Host is "localhost" or "127.0.0.1" or "db" && !config.GetValue("Database:AllowLocalhost", false))
        {
            errors.Add($"ConnectionStrings__Database pointe vers « {db.Host} » : indiquez l'hôte de la base hébergée.");
        }

        var media = config.GetSection(MediaSettings.Section).Get<MediaSettings>() ?? new MediaSettings();
        if (media.IsS3 && (string.IsNullOrWhiteSpace(media.S3.Bucket) || string.IsNullOrWhiteSpace(media.S3.AccessKey) || string.IsNullOrWhiteSpace(media.S3.SecretKey)))
        {
            errors.Add("Media__S3__Bucket, Media__S3__AccessKey et Media__S3__SecretKey sont obligatoires avec Media__Provider=s3.");
        }

        if (media.IsS3 && string.IsNullOrWhiteSpace(media.S3.ServiceUrl) && media.S3.Region == "auto")
        {
            errors.Add("Media__S3__ServiceUrl vide (AWS S3) : indiquez une vraie région dans Media__S3__Region (ex. eu-west-3).");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Configuration de production invalide :\n- " + string.Join("\n- ", errors));
        }
    }

    public static void AddProductionServices(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;

        // Derrière Traefik / Cloud Run : vraie IP du client et schéma https (limites de débit, liens, journaux).
        // Proxy:ForwardLimit = nombre de proxys de confiance devant l'API (1 : Traefik ou Cloud Run seul ; 2 : avec load balancer).
        var forwardLimit = config.GetValue("Proxy:ForwardLimit", 1);
        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            o.ForwardLimit = forwardLimit;
            // Le proxy est dans le réseau Docker privé : son IP n'est pas connue à l'avance.
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        // Photos jusqu'à 10 Mo + marge multipart.
        builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 12 * 1024 * 1024);

        // CORS : liste explicite en production (console admin, app web) ; tout est permis en développement.
        var origins = (config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
        builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        {
            if (builder.Environment.IsDevelopment())
            {
                p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            }
            else
            {
                p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            }
        }));

        var limits = config.GetSection("RateLimiting");
        var enabled = limits.GetValue("Enabled", true);
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.ContentType = "application/problem+json";
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    title = "Trop de requêtes",
                    status = 429,
                    detail = "Trop de tentatives. Patientez une minute avant de réessayer."
                }, ct);
            };

            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => enabled
                ? RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => Window(limits.GetValue("GlobalPerMinute", 600), TimeSpan.FromMinutes(1)))
                : RateLimitPartition.GetNoLimiter("off"));

            // Connexion, inscription, codes : freine le bourrinage de mots de passe et de codes à 6 chiffres.
            o.AddPolicy(RateLimits.Auth, ctx => enabled
                ? RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => Window(limits.GetValue("AuthPerMinute", 10), TimeSpan.FromMinutes(1)))
                : RateLimitPartition.GetNoLimiter("off"));

            o.AddPolicy(RateLimits.Uploads, ctx => enabled
                ? RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => Window(limits.GetValue("UploadsPerHour", 60), TimeSpan.FromHours(1)))
                : RateLimitPartition.GetNoLimiter("off"));
        });

        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

        if (!builder.Environment.IsDevelopment())
        {
            // Journaux JSON : lisibles par Loki, Datadog, docker logs | jq…
            builder.Logging.ClearProviders();
            builder.Logging.AddJsonConsole(o => o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ");
        }
    }

    public static void UseProductionPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.Use(async (ctx, next) =>
        {
            var h = ctx.Response.Headers;
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            await next();
        });
        app.UseCors();
        app.UseRateLimiter();
    }

    private static FixedWindowRateLimiterOptions Window(int permits, TimeSpan window) =>
        new() { PermitLimit = permits, Window = window, QueueLimit = 0 };

    private static string ClientKey(HttpContext ctx) =>
        ctx.User.FindFirst("sub")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "inconnu";
}

/// <summary>/health : chaque base de module joignable.</summary>
public sealed class DatabaseHealthCheck(IServiceProvider services, IEnumerable<ModuleDatabase> databases) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        foreach (var module in databases)
        {
            if (!await ((DbContext)services.GetRequiredService(module.ContextType)).Database.CanConnectAsync(ct))
            {
                return HealthCheckResult.Unhealthy($"Base du module {module.Module} injoignable");
            }
        }

        return HealthCheckResult.Healthy();
    }
}
