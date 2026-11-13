using Microsoft.AspNetCore.Mvc;
using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Services.Application;
using BeninConnect.Modules.Services.Contracts;
using BeninConnect.Modules.Services.Domain;
using BeninConnect.Modules.Services.Infrastructure;
using BeninConnect.Modules.Services.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Services.Api;

public sealed record ProviderRequest(ProviderDetails Profile, bool QualificationDeclared);

public sealed record ServiceRequestBody(Guid OfferingId, string Message, DateOnly? PreferredDate, string? City);

public sealed record ReviewBody(int Rating, string? Comment);

public sealed record ReplyBody(string Reply);

public sealed record ServiceReportBody(ReportTarget TargetType, Guid TargetId, string Reason);

public sealed record VisibilityBody(string? Reason);

public sealed record DecisionBody(string? Reason);

public sealed record CategoryBody(string Slug, string Name, string Icon, Guid? ParentId, int SortOrder, bool IsActive, bool RequiresQualification);

public sealed record ServicesSettingsBody(IReadOnlyList<string> AlertKeywords, int ReportThreshold, int DailyRequestLimit);

/// <summary>Services entre membres : mise en relation, demandes, avis vérifiés. Aucun paiement dans l'application.</summary>
public sealed class ServicesModule : IModule
{
    public string Name => "Services";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<ServicesDbContext>(builder.Configuration, Name, ServicesDbContext.SchemaName);
        services.AddScoped<IServicesDb>(sp => sp.GetRequiredService<ServicesDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Services");
        var options = builder.Configuration.GetSection(ServicesOptions.Section).Get<ServicesOptions>() ?? new ServicesOptions();
        services.AddSingleton(options);
        services.AddScoped<ReviewReminders>();
        services.AddHostedService<ServicesWorker>();
        services.AddScoped<ServicesModuleService>();
        services.AddScoped<IServicesModule>(sp => sp.GetRequiredService<ServicesModuleService>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<ServicesModuleService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/services").WithTags("Services").RequireAuthorization();

        // Recherche et fiches
        api.MapGet("/categories", (ISender s, CancellationToken ct) => s.Send(new GetServiceCategoriesQuery(), ct));
        api.MapGet("/info", (ISender s, CancellationToken ct) => s.Send(new GetServicesInfoQuery(), ct));
        api.MapGet("/search", (Guid? categoryId, string? q, double? lat, double? lng, double? radiusKm, string? city, bool? atHome, double? minRating,
                decimal? priceMax, bool? verifiedOnly, ServiceSort? sort, string? cursor, int? take, ISender s, CancellationToken ct) =>
            s.Send(new SearchServicesQuery(categoryId, q, lat, lng, radiusKm, city, atHome, minRating, priceMax, verifiedOnly, sort ?? ServiceSort.Relevance,
                cursor, take ?? 20), ct));
        api.MapGet("/providers/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetProviderQuery(id), ct));
        api.MapGet("/providers/{id:guid}/reviews", (Guid id, string? cursor, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetProviderReviewsQuery(id, cursor, take ?? 10), ct));
        api.MapPost("/providers/{id:guid}/favorite", (Guid id, ISender s, CancellationToken ct) => s.Send(new SetProviderFavoriteCommand(id, true), ct));
        api.MapDelete("/providers/{id:guid}/favorite", (Guid id, ISender s, CancellationToken ct) => s.Send(new SetProviderFavoriteCommand(id, false), ct));
        api.MapGet("/favorites", (ISender s, CancellationToken ct) => s.Send(new GetFavoriteProvidersQuery(), ct));

        // Espace prestataire
        api.MapGet("/me", (ISender s, CancellationToken ct) => s.Send(new GetMyProviderQuery(), ct));
        api.MapPut("/me", (ProviderRequest r, ISender s, CancellationToken ct) => s.Send(new SaveMyProviderCommand(r.Profile, r.QualificationDeclared), ct));
        api.MapPost("/me/submit", (ISender s, CancellationToken ct) => s.Send(new SubmitMyProviderCommand(), ct));
        api.MapPost("/me/offerings", (OfferingDetails r, ISender s, CancellationToken ct) => s.Send(new SaveOfferingCommand(null, r), ct));
        api.MapPut("/me/offerings/{id:guid}", (Guid id, OfferingDetails r, ISender s, CancellationToken ct) => s.Send(new SaveOfferingCommand(id, r), ct));
        api.MapDelete("/me/offerings/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new DeleteOfferingCommand(id), ct));
        api.MapPost("/images", async (IFormFile? file, ISender s, CancellationToken ct) =>
            {
                await using var content = file?.OpenReadStream() ?? Stream.Null;
                return await s.Send(new UploadServiceImageCommand(content, file?.Length ?? 0), ct);
            })
            .RequireRateLimiting(RateLimits.Uploads).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(6 * 1024 * 1024));
        api.MapPost("/me/documents", async (IFormFile? file, [FromForm] DocumentKind kind, ISender s, CancellationToken ct) =>
            {
                await using var content = file?.OpenReadStream() ?? Stream.Null;
                using var buffer = new MemoryStream();
                await content.CopyToAsync(buffer, ct);
                buffer.Position = 0;
                return await s.Send(new UploadDocumentCommand(kind, buffer, buffer.Length, file?.FileName), ct);
            })
            .RequireRateLimiting(RateLimits.Uploads).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(21 * 1024 * 1024));
        api.MapDelete("/me/documents/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new DeleteDocumentCommand(id), ct));

        // Demandes et avis
        api.MapPost("/requests", (ServiceRequestBody r, ISender s, CancellationToken ct) =>
            s.Send(new CreateServiceRequestCommand(r.OfferingId, r.Message, r.PreferredDate, r.City), ct));
        api.MapGet("/requests", (RequestRole? role, ISender s, CancellationToken ct) => s.Send(new GetServiceRequestsQuery(role ?? RequestRole.Client), ct));
        api.MapGet("/requests/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetServiceRequestQuery(id), ct));
        api.MapPost("/requests/{id:guid}/accept", (Guid id, ISender s, CancellationToken ct) => s.Send(new AcceptServiceRequestCommand(id), ct));
        api.MapPost("/requests/{id:guid}/decline", (Guid id, ReasonRequest r, ISender s, CancellationToken ct) =>
            s.Send(new DeclineServiceRequestCommand(id, r.Reason), ct));
        api.MapPost("/requests/{id:guid}/complete", (Guid id, ISender s, CancellationToken ct) => s.Send(new CompleteServiceRequestCommand(id), ct));
        api.MapPost("/requests/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelServiceRequestCommand(id), ct));
        api.MapPost("/requests/{id:guid}/review", (Guid id, ReviewBody r, ISender s, CancellationToken ct) =>
            s.Send(new CreateReviewCommand(id, r.Rating, r.Comment), ct));
        api.MapPut("/reviews/{id:guid}", (Guid id, ReviewBody r, ISender s, CancellationToken ct) => s.Send(new EditReviewCommand(id, r.Rating, r.Comment), ct));
        api.MapPost("/reviews/{id:guid}/reply", (Guid id, ReplyBody r, ISender s, CancellationToken ct) => s.Send(new ReplyReviewCommand(id, r.Reply), ct));
        api.MapPost("/reports", (ServiceReportBody r, ISender s, CancellationToken ct) =>
            s.Send(new ReportServiceCommand(r.TargetType, r.TargetId, r.Reason), ct));

        MapAdmin(app.MapGroup("/api/admin/services").WithTags("Administration"));
    }

    private static void MapAdmin(RouteGroupBuilder admin)
    {
        const Permission p = Permission.ServicesManage;
        admin.MapGet("", (ISender s, CancellationToken ct) => s.Send(new GetServicesOverviewQuery(), ct)).RequirePermission(p);
        admin.MapGet("/stats", (ISender s, CancellationToken ct) => s.Send(new GetServicesStatsQuery(), ct)).RequirePermission(p);

        admin.MapGet("/providers", (ProviderView? view, string? q, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminProvidersQuery(view ?? ProviderView.Review, q, skip ?? 0, take ?? 25), ct)).RequirePermission(p);
        admin.MapGet("/providers/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetAdminProviderQuery(id), ct)).RequirePermission(p);
        admin.MapPost("/providers/{id:guid}/approve", (Guid id, ISender s, CancellationToken ct) => s.Send(new ApproveProviderCommand(id), ct)).RequirePermission(p);
        admin.MapPost("/providers/{id:guid}/reject", (Guid id, DecisionBody r, ISender s, CancellationToken ct) =>
            s.Send(new RejectProviderCommand(id, r.Reason ?? ""), ct)).RequirePermission(p);
        admin.MapPost("/providers/{id:guid}/suspend", (Guid id, DecisionBody r, ISender s, CancellationToken ct) =>
            s.Send(new SuspendProviderCommand(id, r.Reason ?? ""), ct)).RequirePermission(p);
        admin.MapPost("/providers/{id:guid}/reinstate", (Guid id, ISender s, CancellationToken ct) => s.Send(new ReinstateProviderCommand(id), ct)).RequirePermission(p);
        admin.MapPost("/providers/{id:guid}/revoke-verification", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new RevokeVerificationCommand(id), ct)).RequirePermission(p);
        admin.MapPost("/providers/{id:guid}/delete", async (Guid id, DecisionBody r, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteProviderCommand(id, r.Reason ?? ""), ct);
            return Results.NoContent();
        }).RequirePermission(Permission.ServicesDelete);

        admin.MapGet("/offerings", (ModerationView? view, string? q, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminOfferingsQuery(view ?? ModerationView.Review, q, skip ?? 0, take ?? 25), ct)).RequirePermission(p);
        admin.MapPost("/offerings/{id:guid}/show", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new SetOfferingVisibilityCommand(id, true, null), ct)).RequirePermission(p);
        admin.MapPost("/offerings/{id:guid}/hide", (Guid id, VisibilityBody r, ISender s, CancellationToken ct) =>
            s.Send(new SetOfferingVisibilityCommand(id, false, r.Reason), ct)).RequirePermission(p);

        admin.MapGet("/reviews", (ModerationView? view, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminReviewsQuery(view ?? ModerationView.Review, skip ?? 0, take ?? 25), ct)).RequirePermission(p);
        admin.MapPost("/reviews/{id:guid}/show", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new SetReviewVisibilityCommand(id, true, null), ct)).RequirePermission(p);
        admin.MapPost("/reviews/{id:guid}/hide", (Guid id, VisibilityBody r, ISender s, CancellationToken ct) =>
            s.Send(new SetReviewVisibilityCommand(id, false, r.Reason), ct)).RequirePermission(p);

        admin.MapGet("/reports", (bool? handled, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminServiceReportsQuery(handled ?? false, skip ?? 0, take ?? 25), ct)).RequirePermission(p);
        admin.MapPost("/reports/{id:guid}/close", (Guid id, DecisionBody r, ISender s, CancellationToken ct) =>
            s.Send(new CloseServiceReportCommand(id, r.Reason), ct)).RequirePermission(p);

        admin.MapGet("/documents", (DocumentStatus? status, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminDocumentsQuery(status ?? DocumentStatus.Pending, skip ?? 0, take ?? 25), ct)).RequirePermission(p);
        admin.MapGet("/documents/{id:guid}/file", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var file = await s.Send(new GetDocumentFileQuery(id), ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequirePermission(p);
        admin.MapPost("/documents/{id:guid}/approve", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new DecideDocumentCommand(id, true, null), ct)).RequirePermission(p);
        admin.MapPost("/documents/{id:guid}/reject", (Guid id, DecisionBody r, ISender s, CancellationToken ct) =>
            s.Send(new DecideDocumentCommand(id, false, r.Reason), ct)).RequirePermission(p);

        admin.MapGet("/categories", (ISender s, CancellationToken ct) => s.Send(new GetAdminCategoriesQuery(), ct)).RequirePermission(p);
        admin.MapPost("/categories", (CategoryBody r, ISender s, CancellationToken ct) =>
            s.Send(new SaveCategoryCommand(null, r.Slug, r.Name, r.Icon, r.ParentId, r.SortOrder, r.IsActive, r.RequiresQualification), ct)).RequirePermission(p);
        admin.MapPut("/categories/{id:guid}", (Guid id, CategoryBody r, ISender s, CancellationToken ct) =>
            s.Send(new SaveCategoryCommand(id, r.Slug, r.Name, r.Icon, r.ParentId, r.SortOrder, r.IsActive, r.RequiresQualification), ct)).RequirePermission(p);
        admin.MapPut("/settings", (ServicesSettingsBody r, ISender s, CancellationToken ct) =>
            s.Send(new UpdateServicesSettingsCommand(r.AlertKeywords ?? [], r.ReportThreshold, r.DailyRequestLimit), ct)).RequirePermission(p);
    }
}
