using System.Collections.Concurrent;
using System.Diagnostics;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;

namespace BeninConnect.BackgroundJobs.Scheduling;

public enum JobRunStatus
{
    Succeeded,
    Failed
}

/// <summary>Trigger : « Planifiée » ou « Manuelle : nom de l'administrateur ».</summary>
public sealed record JobRunDto(DateTimeOffset StartedAt, DateTimeOffset FinishedAt, long DurationMs, JobRunStatus Status, string Summary, string Trigger);

public sealed record BackgroundJobDto(
    string Key, string Name, string Module, string Description, string Schedule,
    bool Enabled, bool Scheduled, bool Running, DateTimeOffset? RunningSince, DateTimeOffset? NextRunAt,
    JobRunDto? LastRun, IReadOnlyList<JobRunDto> History);

public sealed record BackgroundJobsOverviewDto(bool SchedulerEnabled, IReadOnlyList<BackgroundJobDto> Jobs);

/// <summary>
/// Exécute les tâches (une seule exécution à la fois par tâche) et garde leur état en mémoire :
/// prochaine exécution, en cours, 10 dernières exécutions. L'historique repart à zéro au redémarrage de l'API.
/// </summary>
public sealed class JobRunner(
    IEnumerable<IBackgroundJob> definitions, IServiceScopeFactory scopes, BackgroundJobsOptions options,
    TimeProvider clock, IHostApplicationLifetime lifetime, ILogger<JobRunner> log)
{
    private const int HistorySize = 10;

    private sealed class State
    {
        public int Running;
        public DateTimeOffset? RunningSince;
        public DateTimeOffset? NextRunAt;
        public readonly LinkedList<JobRunDto> History = new();
    }

    private readonly ConcurrentDictionary<string, State> _states = new();

    /// <summary>Tâches dont le module a enregistré l'IScheduledTask (les autres, ex. remise à zéro hors Review, sont ignorées).</summary>
    public IReadOnlyList<IBackgroundJob> Jobs { get; } = Available(definitions, scopes);

    private static List<IBackgroundJob> Available(IEnumerable<IBackgroundJob> definitions, IServiceScopeFactory scopes)
    {
        var duplicates = definitions.GroupBy(d => d.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException($"Tâches planifiées en double : {string.Join(", ", duplicates)}.");
        }

        using var scope = scopes.CreateScope();
        var registered = scope.ServiceProvider.GetRequiredService<IServiceProviderIsKeyedService>();
        return definitions.Where(d => registered.IsKeyedService(typeof(IScheduledTask), d.Key)).OrderBy(d => d.Module).ThenBy(d => d.Name).ToList();
    }

    public IBackgroundJob? Find(string key) => Jobs.FirstOrDefault(j => j.Key == key);

    private State StateOf(string key) => _states.GetOrAdd(key, _ => new State());

    public bool IsScheduled(IBackgroundJob job) => options.Enabled && !options.Disabled.Contains(job.Key);

    public void SetNextRun(IBackgroundJob job, DateTimeOffset? next) => StateOf(job.Key).NextRunAt = next;

    public DateTimeOffset? NextRun(IBackgroundJob job) => StateOf(job.Key).NextRunAt;

    public bool IsRunning(IBackgroundJob job) => Volatile.Read(ref StateOf(job.Key).Running) == 1;

    /// <summary>Tâche désactivée par la configuration de son module.</summary>
    public bool IsEnabled(IBackgroundJob job)
    {
        using var scope = scopes.CreateScope();
        return scope.ServiceProvider.GetRequiredKeyedService<IScheduledTask>(job.Key).Enabled;
    }

    /// <summary>Lancement manuel en arrière-plan (la requête HTTP n'attend pas la fin).</summary>
    public Task StartManualAsync(IBackgroundJob job, string adminName)
    {
        if (!IsEnabled(job))
        {
            throw new ConflictException("Tâche désactivée par la configuration du module.");
        }

        if (IsRunning(job))
        {
            throw new ConflictException("Cette tâche est déjà en cours.");
        }

        return Task.Run(() => RunAsync(job, manual: true, $"Manuelle : {adminName}", lifetime.ApplicationStopping));
    }

    /// <summary>false : déjà en cours (exécution ignorée).</summary>
    public async Task<bool> RunAsync(IBackgroundJob job, bool manual, string trigger, CancellationToken ct)
    {
        var state = StateOf(job.Key);
        if (Interlocked.CompareExchange(ref state.Running, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            var started = clock.GetUtcNow();
            state.RunningSince = started;
            var watch = Stopwatch.StartNew();
            JobRunStatus status;
            string summary;
            try
            {
                using var scope = scopes.CreateScope();
                var task = scope.ServiceProvider.GetRequiredKeyedService<IScheduledTask>(job.Key);
                if (!task.Enabled)
                {
                    return true;
                }

                summary = await task.RunAsync(new ScheduledTaskRun(manual), ct);
                status = JobRunStatus.Succeeded;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                status = JobRunStatus.Failed;
                summary = "Interrompue (arrêt de l'API).";
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Tâche planifiée {Job} en échec", job.Key);
                status = JobRunStatus.Failed;
                summary = ex is DomainException or InvalidOperationException ? ex.Message : $"Erreur : {ex.Message}";
            }

            var run = new JobRunDto(started, clock.GetUtcNow(), watch.ElapsedMilliseconds, status, Truncate(summary), trigger);
            lock (state.History)
            {
                state.History.AddFirst(run);
                while (state.History.Count > HistorySize)
                {
                    state.History.RemoveLast();
                }
            }

            return true;
        }
        finally
        {
            state.RunningSince = null;
            Volatile.Write(ref state.Running, 0);
        }
    }

    public BackgroundJobDto Describe(IBackgroundJob job)
    {
        var state = StateOf(job.Key);
        List<JobRunDto> history;
        lock (state.History)
        {
            history = state.History.ToList();
        }

        var scheduled = IsScheduled(job);
        var enabled = IsEnabled(job);
        return new BackgroundJobDto(job.Key, job.Name, job.Module, job.Description, job.Schedule.Describe(), enabled, scheduled,
            IsRunning(job), state.RunningSince, scheduled && enabled ? state.NextRunAt : null, history.FirstOrDefault(), history);
    }

    public BackgroundJobsOverviewDto Overview() => new(options.Enabled, Jobs.Select(Describe).ToList());

    private static string Truncate(string text) => text.Length <= 500 ? text : text[..500] + "…";
}
