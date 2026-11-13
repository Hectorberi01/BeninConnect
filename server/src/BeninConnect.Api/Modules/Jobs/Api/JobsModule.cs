using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Jobs.Application;
using BeninConnect.Modules.Jobs.Contracts;
using BeninConnect.Modules.Jobs.Domain;
using BeninConnect.Modules.Jobs.Infrastructure;
using BeninConnect.Modules.Jobs.Infrastructure.Persistence;
using BeninConnect.Modules.Jobs.Infrastructure.Sources;
using BeninConnect.Shared.Api;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Jobs.Api;

public sealed record JobSourceRequest(bool IsEnabled, int ImportIntervalMinutes, string? ClientId, IReadOnlyList<JobSearchDefinition>? Searches);

public sealed record SecretRequest(string? Secret);

public sealed record JobSettingsRequest(IReadOnlyList<string> AlertKeywords, int ReportThreshold);

/// <summary>Rubrique Emploi : offres importées d'API externes (France Travail, ReliefWeb…), candidature chez la source.</summary>
public sealed class JobsModule : IModule
{
    public string Name => "Jobs";

    public void AddServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<JobsDbContext>(builder.Configuration, Name, JobsDbContext.SchemaName);
        services.AddScoped<IJobsDb>(sp => sp.GetRequiredService<JobsDbContext>());
        services.AddModuleHandlers("BeninConnect.Modules.Jobs");

        var options = builder.Configuration.GetSection(JobsOptions.Section).Get<JobsOptions>() ?? new JobsOptions();
        // Environnement de revue Apple / Google : jamais d'import
        if (builder.Environment.IsEnvironment("Review"))
        {
            options.WorkerEnabled = false;
        }

        services.AddSingleton(options);
        services.AddSingleton<FranceTravailTokens>();
        services.AddSingleton<ISecretProtector, SecretProtector>();
        services.AddScoped<SourceHttp>();
        foreach (var name in new[] { FranceTravailSource.ClientName, ReliefWebSource.ClientName, AdzunaSource.ClientName, RemotiveSource.ClientName })
        {
            services.AddHttpClient(name, c =>
            {
                c.Timeout = TimeSpan.FromSeconds(30);
                c.DefaultRequestHeaders.UserAgent.ParseAdd("BeninConnect-Jobs/1.0 (+https://monbenin.com)");
            });
        }

        services.AddScoped<IJobOfferSource, FranceTravailSource>();
        services.AddScoped<IJobOfferSource, ReliefWebSource>();
        services.AddScoped<IJobOfferSource, AdzunaSource>();
        services.AddScoped<IJobOfferSource, RemotiveSource>();
        services.AddScoped<JobImporter>();
        services.AddHostedService<JobsWorker>();
        services.AddScoped<JobsModuleService>();
        services.AddScoped<IJobsModule>(sp => sp.GetRequiredService<JobsModuleService>());
        services.AddScoped<IModuleStatistics>(sp => sp.GetRequiredService<JobsModuleService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs").WithTags("Emploi").RequireAuthorization();
        jobs.MapGet("", (string? q, string? country, string? department, ContractType? contract, bool? remote, JobSource? source, string? cursor,
            int? take, ISender s, CancellationToken ct) =>
            s.Send(new JobSearchQuery(q, country, department, contract, remote, source, cursor, take ?? 20), ct));
        jobs.MapGet("/filters", (ISender s, CancellationToken ct) => s.Send(new GetJobFiltersQuery(), ct));
        jobs.MapGet("/favorites", (ISender s, CancellationToken ct) => s.Send(new GetFavoriteJobsQuery(), ct));
        jobs.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetJobOfferQuery(id), ct));
        jobs.MapPost("/{id:guid}/favorite", (Guid id, ISender s, CancellationToken ct) => s.Send(new SetJobFavoriteCommand(id, true), ct));
        jobs.MapDelete("/{id:guid}/favorite", (Guid id, ISender s, CancellationToken ct) => s.Send(new SetJobFavoriteCommand(id, false), ct));
        jobs.MapPost("/{id:guid}/report", (Guid id, ReasonRequest r, ISender s, CancellationToken ct) =>
            s.Send(new ReportJobOfferCommand(id, r.Reason ?? string.Empty), ct));

        var admin = app.MapGroup("/api/admin/jobs").WithTags("Administration");
        admin.MapGet("", (ISender s, CancellationToken ct) => s.Send(new GetJobsAdminOverviewQuery(), ct)).RequirePermission(Permission.JobsManage);
        admin.MapPut("/sources/{source}", (JobSource source, JobSourceRequest r, ISender s, CancellationToken ct) =>
                s.Send(new UpdateJobSourceCommand(source, r.IsEnabled, r.ImportIntervalMinutes, r.ClientId, r.Searches), ct))
            .RequirePermission(Permission.JobsManage);
        admin.MapPut("/sources/{source}/secret", (JobSource source, SecretRequest r, ISender s, CancellationToken ct) =>
            s.Send(new SetJobSourceSecretCommand(source, r.Secret), ct)).RequirePermission(Permission.JobsManage);
        admin.MapPost("/sources/{source}/run", (JobSource source, ISender s, CancellationToken ct) =>
            s.Send(new RunJobImportCommand(source), ct)).RequirePermission(Permission.JobsManage);
        admin.MapPut("/settings", (JobSettingsRequest r, ISender s, CancellationToken ct) =>
            s.Send(new UpdateJobSettingsCommand(r.AlertKeywords ?? [], r.ReportThreshold), ct)).RequirePermission(Permission.JobsManage);
        admin.MapGet("/offers", (AdminOfferView? view, string? q, JobSource? source, int? skip, int? take, ISender s, CancellationToken ct) =>
            s.Send(new GetAdminJobOffersQuery(view ?? AdminOfferView.Review, q, source, skip ?? 0, take ?? 25), ct)).RequirePermission(Permission.JobsManage);
        admin.MapPost("/offers/{id:guid}/show", (Guid id, ISender s, CancellationToken ct) => s.Send(new ShowJobOfferCommand(id), ct))
            .RequirePermission(Permission.JobsManage);
        admin.MapPost("/offers/{id:guid}/hide", (Guid id, ISender s, CancellationToken ct) => s.Send(new HideJobOfferCommand(id), ct))
            .RequirePermission(Permission.JobsManage);
    }
}
