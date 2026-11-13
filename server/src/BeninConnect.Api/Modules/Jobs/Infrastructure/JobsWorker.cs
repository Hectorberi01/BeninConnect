using BeninConnect.Modules.Jobs.Application;
using BeninConnect.Modules.Jobs.Domain;

namespace BeninConnect.Modules.Jobs.Infrastructure;

/// <summary>Imports automatiques : chaque minute, lance les sources activées dont l'intervalle est écoulé.</summary>
public sealed class JobsWorker(IServiceScopeFactory scopes, JobsOptions options, TimeProvider clock, ILogger<JobsWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.WorkerEnabled)
        {
            log.LogInformation("Emploi : imports automatiques désactivés (Jobs__WorkerEnabled).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            IReadOnlyList<JobSource> due;
            try
            {
                using var scope = scopes.CreateScope();
                due = await scope.ServiceProvider.GetRequiredService<JobImporter>().DueSourcesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Emploi : lecture de la configuration impossible");
                continue;
            }

            // Une source en erreur ne bloque jamais les autres
            foreach (var source in due)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<JobImporter>().RunAsync(source, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Emploi : import {Source} en échec", source);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
