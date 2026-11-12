using BeninConnect.Modules.Editorial.Application;

namespace BeninConnect.Modules.Editorial.Infrastructure;

/// <summary>Tâches automatiques : collecte, rédaction, publication des contenus validés. Active seulement si Editorial:Enabled.</summary>
public sealed class EditorialWorker(IServiceScopeFactory scopes, EditorialSettings settings, TimeProvider clock, ILogger<EditorialWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            log.LogInformation("Éditorial : tâches automatiques désactivées (Editorial__Enabled).");
            return;
        }

        var next = new Dictionary<string, DateTimeOffset>();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            await RunIfDueAsync("publish", settings.PublishEveryMinutes, p => p.PublishDueAsync(stoppingToken), next, stoppingToken);
            await RunIfDueAsync("collect", settings.CollectEveryMinutes, p => p.CollectAsync(stoppingToken), next, stoppingToken);
            await RunIfDueAsync("draft", settings.DraftEveryMinutes, p => p.DraftAsync(stoppingToken), next, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunIfDueAsync<T>(string step, int everyMinutes, Func<EditorialPipeline, Task<T>> run,
        Dictionary<string, DateTimeOffset> next, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (next.TryGetValue(step, out var due) && due > now)
        {
            return;
        }

        next[step] = now.AddMinutes(Math.Max(1, everyMinutes));
        try
        {
            using var scope = scopes.CreateScope();
            await run(scope.ServiceProvider.GetRequiredService<EditorialPipeline>());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Éditorial : étape {Step} en échec", step);
        }
    }
}
