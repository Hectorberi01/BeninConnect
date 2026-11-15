using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Administration.Contracts;

namespace BeninConnect.BackgroundJobs;

/// <summary>Tâches de fond de toute l'API : définitions (Jobs/), planificateur unique et page « Tâches planifiées » de la console.</summary>
public static class BackgroundJobsSetup
{
    public static void AddBackgroundJobs(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSingleton(builder.Configuration.GetSection(BackgroundJobsOptions.Section).Get<BackgroundJobsOptions>() ?? new BackgroundJobsOptions());

        // Un fichier par tâche dans Jobs/ : toutes les implémentations d'IBackgroundJob de ce dossier
        foreach (var type in typeof(BackgroundJobsSetup).Assembly.GetTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBackgroundJob).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(IBackgroundJob), type);
        }

        services.AddSingleton<JobRunner>();
        services.AddHostedService<JobScheduler>();
    }

    public static void MapBackgroundJobs(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/background-jobs").WithTags("Administration").RequirePermission(Permission.BackgroundJobsManage);

        admin.MapGet("", (JobRunner runner) => runner.Overview());

        admin.MapPost("/{key}/run", async (string key, JobRunner runner, IAdminSession session, CancellationToken ct) =>
        {
            var job = runner.Find(key);
            if (job is null)
            {
                return Results.NotFound();
            }

            var me = await session.GetAsync(ct);
            _ = runner.StartManualAsync(job, me.DisplayName);
            await session.LogAsync("Tâche planifiée lancée manuellement", "BackgroundJob", null, $"{job.Name} ({job.Key})", ct);
            return Results.Accepted(value: runner.Describe(job));
        });
    }
}
