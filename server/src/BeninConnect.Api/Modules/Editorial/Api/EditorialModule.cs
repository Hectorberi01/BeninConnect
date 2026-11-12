using Microsoft.AspNetCore.Mvc;
using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Editorial.Application;
using BeninConnect.Modules.Editorial.Contracts;
using BeninConnect.Modules.Editorial.Domain;
using BeninConnect.Modules.Editorial.Infrastructure;
using BeninConnect.Modules.Editorial.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Editorial.Api;

public sealed record DraftRequest(string Content, IReadOnlyList<string>? Images = null);

public sealed record ApproveRequest(string Content, DateTimeOffset? ScheduledAt, Guid? CommunityId, IReadOnlyList<string>? Images);

public sealed record RejectRequest(string? Reason);

public sealed record SourceRequest(string Name, string Url, bool IsEnabled = true);

/// <summary>Compte officiel : sources RSS, brouillons rédigés par Claude, validation dans la console, publication programmée.</summary>
public sealed class EditorialModule : IModule
{
    public string Name => "Editorial";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<EditorialDbContext>(builder.Configuration, Name, EditorialDbContext.SchemaName);
        services.AddScoped<IEditorialDb>(sp => sp.GetRequiredService<EditorialDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Editorial");

        var settings = builder.Configuration.GetSection(EditorialSettings.Section).Get<EditorialSettings>() ?? new EditorialSettings();
        // Environnement de revue Apple / Google : jamais de tâches automatiques ni d'appels au LLM
        if (builder.Environment.IsEnvironment("Review"))
        {
            settings.Enabled = false;
            settings.Llm.ApiKey = null;
        }

        services.AddSingleton(settings);
        services.AddHttpClient(XmlFeedReader.ClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("BeninConnect-Editorial/1.0 (+https://monbenin.com)");
        });
        services.AddHttpClient(ClaudeLlmClient.ClientName, c => c.Timeout = TimeSpan.FromSeconds(60));
        services.AddScoped<IFeedReader, XmlFeedReader>();
        services.AddScoped<ILlmClient, ClaudeLlmClient>();
        services.AddScoped<EditorialPipeline>();
        services.AddHostedService<EditorialWorker>();
        services.AddScoped<EditorialModuleService>();
        services.AddScoped<IEditorialModule>(sp => sp.GetRequiredService<EditorialModuleService>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<EditorialModuleService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/editorial").WithTags("Administration");

        admin.MapGet("", (ISender s, CancellationToken ct) => s.Send(new GetEditorialOverviewQuery(), ct)).RequirePermission(Permission.EditorialManage);
        admin.MapGet("/candidates", (CandidateStatus? status, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetCandidatesQuery(status, skip ?? 0, take ?? 25), ct)).RequirePermission(Permission.EditorialManage);
        admin.MapPost("/candidates", (DraftRequest r, ISender s, CancellationToken ct) =>
            s.Send(new CreateManualDraftCommand(r.Content, r.Images), ct)).RequirePermission(Permission.EditorialManage);
        admin.MapPut("/candidates/{id:guid}/draft", (Guid id, DraftRequest r, ISender s, CancellationToken ct) =>
            s.Send(new EditDraftCommand(id, r.Content, r.Images), ct)).RequirePermission(Permission.EditorialManage);

        // Photos des publications officielles (même stockage et mêmes contrôles que l'application)
        admin.MapPost("/images", async (IFormFile? file, ISender s, CancellationToken ct) =>
            {
                await using var content = file?.OpenReadStream() ?? Stream.Null;
                return await s.Send(new UploadEditorialImageCommand(content, file?.Length ?? 0), ct);
            })
            .RequirePermission(Permission.EditorialManage)
            .RequireRateLimiting(RateLimits.Uploads)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(11 * 1024 * 1024));
        admin.MapPost("/candidates/{id:guid}/approve", (Guid id, ApproveRequest r, ISender s, CancellationToken ct) =>
            s.Send(new ApproveCandidateCommand(id, r.Content, r.ScheduledAt, r.CommunityId, r.Images), ct)).RequirePermission(Permission.EditorialManage);
        admin.MapPost("/candidates/{id:guid}/reject", (Guid id, RejectRequest r, ISender s, CancellationToken ct) =>
            s.Send(new RejectCandidateCommand(id, r.Reason), ct)).RequirePermission(Permission.EditorialManage);
        admin.MapPost("/candidates/{id:guid}/unschedule", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new UnscheduleCandidateCommand(id), ct)).RequirePermission(Permission.EditorialManage);
        admin.MapPost("/candidates/{id:guid}/retry", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new RetryCandidateCommand(id), ct)).RequirePermission(Permission.EditorialManage);
        admin.MapPost("/run/{step}", (string step, ISender s, CancellationToken ct) =>
            s.Send(new RunEditorialStepCommand(step), ct)).RequirePermission(Permission.EditorialManage);

        admin.MapPost("/sources", (SourceRequest r, ISender s, CancellationToken ct) =>
            s.Send(new AddSourceCommand(r.Name, r.Url), ct)).RequirePermission(Permission.EditorialManage);
        admin.MapPut("/sources/{id:guid}", (Guid id, SourceRequest r, ISender s, CancellationToken ct) =>
            s.Send(new UpdateSourceCommand(id, r.Name, r.Url, r.IsEnabled), ct)).RequirePermission(Permission.EditorialManage);
        admin.MapDelete("/sources/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new DeleteSourceCommand(id), ct)).RequirePermission(Permission.EditorialManage);
    }
}
