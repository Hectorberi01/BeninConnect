namespace BeninConnect.BackgroundJobs.Scheduling;

/// <summary>
/// Planificateur unique : toutes les TickSeconds, lance les tâches dont l'heure est venue. Chaque tâche tourne
/// à part (une tâche longue, comme une sauvegarde, ne retarde pas les autres) et jamais deux fois en même temps.
/// </summary>
public sealed class JobScheduler(JobRunner runner, BackgroundJobsOptions options, TimeProvider clock, ILogger<JobScheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            log.LogInformation("Tâches planifiées désactivées (BackgroundJobs__Enabled).");
            return;
        }

        var jobs = runner.Jobs.Where(j => runner.IsScheduled(j) && runner.IsEnabled(j)).ToList();
        var start = clock.GetUtcNow();
        foreach (var job in jobs)
        {
            runner.SetNextRun(job, job.Schedule.First(start));
        }

        log.LogInformation("Tâches planifiées : {Jobs}", string.Join(", ", jobs.Select(j => j.Key)));
        var running = new List<Task>();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.TickSeconds)), clock);
        try
        {
            do
            {
                var now = clock.GetUtcNow();
                foreach (var job in jobs)
                {
                    if (runner.NextRun(job) is { } due && due <= now && !runner.IsRunning(job))
                    {
                        runner.SetNextRun(job, job.Schedule.Next(now));
                        running.Add(Task.Run(() => runner.RunAsync(job, manual: false, "Planifiée", stoppingToken), stoppingToken));
                    }
                }

                running.RemoveAll(t => t.IsCompleted);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // arrêt de l'application
        }

        await Task.WhenAll(running).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ContinueWith(_ => { }, CancellationToken.None);
    }
}
