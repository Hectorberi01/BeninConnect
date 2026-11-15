using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.AppVersionManagement.Application;
using BeninConnect.Modules.AppVersionManagement.Contracts;
using BeninConnect.Modules.AppVersionManagement.Domain;
using BeninConnect.Modules.AppVersionManagement.Infrastructure;
using BeninConnect.Modules.AppVersionManagement.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.AppVersionManagement.Api;

public sealed record ReleaseBody(string? LatestVersion, string? MinimumSupportedVersion, string StoreUrl);

public sealed record ApproveCampaignBody(bool SendPush, bool SendEmail);

/// <summary>Versions de l'application : version installée par chaque téléphone, mises à jour conseillées ou obligatoires, relances validées par l'équipe.</summary>
public sealed class AppVersionManagementModule : IModule
{
    public string Name => "AppVersionManagement";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<AppVersionsDbContext>(builder.Configuration, Name, AppVersionsDbContext.SchemaName);
        services.AddScoped<IAppVersionsDb>(sp => sp.GetRequiredService<AppVersionsDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.AppVersionManagement");

        var settings = builder.Configuration.GetSection(AppVersionSettings.Section).Get<AppVersionSettings>() ?? new AppVersionSettings();
        settings.LegacyMinimumVersion ??= builder.Configuration["MobileApp:MinimumVersion"];
        services.AddSingleton(settings);
        services.AddScoped<OutdatedDetector>();
        services.AddScoped<CampaignSender>();
        services.AddKeyedScoped<IScheduledTask, DetectOutdatedTask>(AppVersionTasks.DetectOutdated);
        services.AddKeyedScoped<IScheduledTask, SendCampaignsTask>(AppVersionTasks.SendCampaigns);
        services.AddScoped<IModuleStatistics, AppVersionsModuleService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var mobile = app.MapGroup("/api/app").WithTags("Application");
        // Anonyme accepté (avant connexion) ; le membre est lu dans le jeton s'il y en a un
        mobile.MapPost("/installations", (ReportInstallationCommand c, ISender s, CancellationToken ct) => s.Send(c, ct)).AllowAnonymous();

        // Applications d'avant le suivi des installations
        mobile.MapGet("/version", async (string? platform, ISender s, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Ok(new { minimumVersion = await s.Send(new GetMinimumVersionQuery(platform), ct) });
        }).AllowAnonymous();

        var admin = app.MapGroup("/api/admin/app-versions").WithTags("Administration").RequirePermission(Permission.AppVersionsManage);
        admin.MapGet("", (ISender s, CancellationToken ct) => s.Send(new GetAppVersionsOverviewQuery(), ct));
        admin.MapPut("/releases/{platform}", (string platform, ReleaseBody r, ISender s, CancellationToken ct) =>
            s.Send(new UpdateReleaseCommand(platform, r.LatestVersion, r.MinimumSupportedVersion, r.StoreUrl), ct));
        admin.MapGet("/installations", (UpdateStatus? status, string? platform, Guid? userId, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetInstallationsQuery(status, platform, userId, skip ?? 0, take ?? 25), ct));
        admin.MapGet("/campaigns", (int? skip, int? take, ISender s, CancellationToken ct) => s.Send(new GetCampaignsQuery(skip ?? 0, take ?? 20), ct));
        admin.MapGet("/campaigns/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetCampaignQuery(id), ct));
        // campaign null : aucun membre à relancer
        admin.MapPost("/detect", async (ISender s, CancellationToken ct) => new { campaign = await s.Send(new DetectOutdatedCommand(), ct) });
        admin.MapPost("/campaigns/{id:guid}/approve", (Guid id, ApproveCampaignBody r, ISender s, CancellationToken ct) =>
            s.Send(new ApproveCampaignCommand(id, r.SendPush, r.SendEmail), ct));
        admin.MapPost("/campaigns/{id:guid}/reject", (Guid id, ReasonRequest r, ISender s, CancellationToken ct) =>
            s.Send(new RejectCampaignCommand(id, r.Reason), ct));
    }
}
