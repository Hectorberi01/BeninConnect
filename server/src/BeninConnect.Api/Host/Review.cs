using System.Text.Json;
using BeninConnect.Host.DemoData;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BeninConnect.Host;

/// <summary>
/// Environnement de revue Apple / Google : API et base séparées, données de démonstration remises à zéro chaque nuit.
/// En production : seule la liste Emails sert (redirection). En mode Review : compte de revue et remise à zéro.
/// </summary>
public sealed class ReviewSettings
{
    public const string Section = "Review";
    public const string EnvironmentName = "Review";

    /// <summary>Production : emails redirigés vers l'API de revue.</summary>
    public string[] Emails { get; set; } = [];

    /// <summary>Production : adresse de l'API de revue (https://review-api.monbenin.com).</summary>
    public string? ApiUrl { get; set; }

    /// <summary>Mode Review : compte donné à Apple / Google (recréé à chaque remise à zéro).</summary>
    public string? AccountEmail { get; set; }

    public string? AccountPassword { get; set; }

    /// <summary>Mode Review : heure UTC de la remise à zéro quotidienne (vide : jamais).</summary>
    public string? ResetDailyAtUtc { get; set; } = "03:00";

    /// <summary>Emails renseignés (variables vides du compose ignorées).</summary>
    public string[] ActiveEmails => Emails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).ToArray();

    public bool Redirects(string? email) =>
        !string.IsNullOrWhiteSpace(ApiUrl) && email is not null
        && ActiveEmails.Any(e => string.Equals(e, email.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class ReviewEnvironment
{
    public static bool IsReview(this IHostEnvironment env) => env.IsEnvironment(ReviewSettings.EnvironmentName);

    // Requêtes anonymes qui portent l'email d'un compte
    private static readonly string[] AuthPaths =
    [
        "/api/auth/login", "/api/auth/register", "/api/auth/verify-email", "/api/auth/resend-code",
        "/api/auth/forgot-password", "/api/auth/reset-password"
    ];

    /// <summary>
    /// Production : un email de revue reçoit 421 et l'adresse de l'API de revue, sans vérification du mot de passe
    /// ni création de compte. Une ancienne application affiche simplement le message.
    /// </summary>
    public static void UseReviewRedirect(this WebApplication app)
    {
        var settings = app.Configuration.GetSection(ReviewSettings.Section).Get<ReviewSettings>() ?? new ReviewSettings();
        if (app.Environment.IsReview() || string.IsNullOrWhiteSpace(settings.ApiUrl) || settings.ActiveEmails.Length == 0)
        {
            return;
        }

        app.Use(async (ctx, next) =>
        {
            if (!HttpMethods.IsPost(ctx.Request.Method) || !AuthPaths.Contains(ctx.Request.Path.Value, StringComparer.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            ctx.Request.EnableBuffering();
            var email = await ReadEmailAsync(ctx.Request);
            ctx.Request.Body.Position = 0;
            if (!settings.Redirects(email))
            {
                await next();
                return;
            }

            ctx.Response.StatusCode = StatusCodes.Status421MisdirectedRequest;
            await ctx.Response.WriteAsJsonAsync(new
            {
                title = "Compte de démonstration",
                status = 421,
                detail = "Ce compte de démonstration nécessite la dernière version de l'application.",
                redirect = settings.ApiUrl!.TrimEnd('/')
            }, (JsonSerializerOptions?)null, "application/problem+json");
        });
    }

    private static async Task<string?> ReadEmailAsync(HttpRequest request)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(request.Body);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(p.Name, "email", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                {
                    return p.Value.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // corps invalide : l'endpoint répondra lui-même
        }

        return null;
    }
}

/// <summary>Mode Review : vide toutes les tables des modules (schéma et migrations gardés), puis recharge la démo.</summary>
public sealed class ReviewDataReset(IServiceProvider services, IEnumerable<ModuleDatabase> databases, DemoDataSeeder seeder) : IDemoDataReset
{
    public async Task ResetAsync(CancellationToken ct)
    {
        var contexts = databases.Select(d => (DbContext)services.GetRequiredService(d.ContextType)).ToList();
        var tables = contexts
            .SelectMany(db => db.Model.GetEntityTypes())
            .Where(e => e.GetTableName() is not null && e.GetViewName() is null)
            .Select(e => $"\"{e.GetSchema() ?? "public"}\".\"{e.GetTableName()}\"")
            .Distinct()
            .ToList();

        // Une seule instruction : les clés étrangères entre tables ne gênent pas
        await contexts[0].Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE", ct);
        foreach (var db in contexts)
        {
            db.ChangeTracker.Clear();
        }

        await seeder.SeedAsync(ct);
    }
}

/// <summary>Mode Review : remise à zéro quotidienne des données (contenus créés par les testeurs effacés).</summary>
public sealed class ReviewResetWorker(IServiceScopeFactory scopes, IConfiguration config, TimeProvider clock, ILogger<ReviewResetWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = config.GetSection(ReviewSettings.Section).Get<ReviewSettings>() ?? new ReviewSettings();
        if (!TimeOnly.TryParse(settings.ResetDailyAtUtc, out var at))
        {
            logger.LogInformation("Revue : remise à zéro quotidienne désactivée.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = clock.GetUtcNow();
            var next = new DateTimeOffset(DateOnly.FromDateTime(now.UtcDateTime), at, TimeSpan.Zero);
            if (next <= now)
            {
                next = next.AddDays(1);
            }

            await Task.Delay(next - now, clock, stoppingToken);
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IDemoDataReset>().ResetAsync(stoppingToken);
                logger.LogInformation("Revue : données de démonstration remises à zéro.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Revue : échec de la remise à zéro.");
            }
        }
    }
}
