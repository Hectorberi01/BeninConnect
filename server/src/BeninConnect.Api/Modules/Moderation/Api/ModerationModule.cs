using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Moderation.Application;
using BeninConnect.Modules.Moderation.Domain;
using BeninConnect.Modules.Moderation.Contracts;
using BeninConnect.Modules.Moderation.Infrastructure;
using BeninConnect.Modules.Moderation.Infrastructure.Persistence;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Moderation.Api;

public sealed record DecisionRequest(string Decision, string? Note);

/// <summary>Signalements des membres et file de modération de la console.</summary>
public sealed class ModerationModule : IModule
{
    public string Name => "Moderation";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<ModerationDbContext>(builder.Configuration, Name, ModerationDbContext.SchemaName);
        services.AddScoped<IModerationDb>(sp => sp.GetRequiredService<ModerationDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Moderation");
        services.AddScoped<ModerationModuleService>();
        services.AddScoped<IModerationModule>(sp => sp.GetRequiredService<ModerationModuleService>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<ModerationModuleService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/reports", async (CreateReportCommand c, ISender s, CancellationToken ct) =>
            Results.Created("/api/reports", await s.Send(c, ct))).WithTags("Signalements").RequireAuthorization();

        var admin = app.MapGroup("/api/admin/reports").WithTags("Administration");
        admin.MapGet("", (ReportStatus? status, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminReportsQuery(status, skip ?? 0, take ?? 25), ct)).RequirePermission(Permission.ReportsManage);
        admin.MapPost("/{id:guid}/resolve", (Guid id, DecisionRequest r, ISender s, CancellationToken ct) =>
            s.Send(new ResolveReportCommand(id, r.Decision, r.Note), ct)).RequirePermission(Permission.ReportsManage);
    }
}
