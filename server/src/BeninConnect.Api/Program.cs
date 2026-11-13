using System.Text.Json.Serialization;
using BeninConnect.Host;
using BeninConnect.Host.DemoData;
using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Identity.Infrastructure;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration partagée ---
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey doit contenir au moins 32 caractères.");
}

Production.Validate(builder.Configuration, builder.Environment, jwt,
    builder.Configuration.GetSection("Auth").Get<AuthSettings>() ?? new AuthSettings(),
    builder.Configuration.GetSection(EmailSettings.Section).Get<EmailSettings>() ?? new EmailSettings());

// --- Noyau commun ---
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(jwt);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ISender, Sender>();
builder.Services.AddScoped<IEventBus, InMemoryEventBus>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.AddProductionServices();

// --- Modules ---
foreach (var module in ModuleCatalog.All)
{
    module.AddServices(builder);
}

builder.Services.AddScoped<DemoDataSeeder>();
if (builder.Environment.IsReview())
{
    // Revue Apple / Google : compte de revue, tables vidées (et non base supprimée), remise à zéro chaque nuit
    var review = builder.Configuration.GetSection(ReviewSettings.Section).Get<ReviewSettings>()!;
    builder.Services.AddSingleton(new DemoSeedAccount(review.AccountEmail!, review.AccountPassword!, SeedAdmins: false));
    builder.Services.AddScoped<IDemoDataReset, ReviewDataReset>();
    builder.Services.AddHostedService<ReviewResetWorker>();
}
else
{
    builder.Services.AddScoped<IDemoDataReset, DemoDataReset>();
}

var app = builder.Build();

// --- Base de données : migrations de chaque module + données initiales ---
// « --migrate » : applique les migrations puis s'arrête (tâche Cloud Run Job avant déploiement).
var migrateOnly = args.Contains("--migrate");
if (migrateOnly || app.Configuration.GetValue("Database:InitializeOnStartup", true))
{
    await DatabaseStartup.InitializeAsync(app.Services, app.Configuration);
}

if (migrateOnly)
{
    return;
}

// --- Pipeline ---
app.UseProductionPipeline();
app.UseReviewRedirect(); // production : comptes de revue envoyés vers l'API de revue
app.UseExceptionHandler();
app.UseStatusCodePages();

foreach (var module in ModuleCatalog.All.OfType<IModulePipeline>())
{
    module.UsePipeline(app);
}

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json

    // Réinitialise la base avec le jeu de démonstration (dates recalculées par rapport à maintenant).
    app.MapPost("/api/dev/reset", async (IDemoDataReset demo, CancellationToken ct) =>
    {
        await demo.ResetAsync(ct);
        return Results.NoContent();
    }).WithTags("Développement");
}

app.MapHealthChecks("/health");
app.MapAppLinks(); // /.well-known : ouverture des liens monbenin.com dans l'application
app.MapMobileApp(); // version minimale de l'app mobile
app.MapGet("/", () => Results.Ok(new { name = "BéninConnect API", version = AppVersion.Current, docs = "/openapi/v1.json" })).ExcludeFromDescription();

foreach (var module in ModuleCatalog.All)
{
    module.MapEndpoints(app);
}

app.Run();

/// <summary>Point d'entrée exposé aux tests d'intégration (WebApplicationFactory).</summary>
public partial class Program;
