using BeninConnect.Modules.Jobs.Application;
using BeninConnect.Modules.Jobs.Domain;
using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Jobs.Infrastructure;

/// <summary>Imports automatiques : sources activées dont l'intervalle est écoulé (toutes les sources activées si lancé à la main).</summary>
public sealed class JobsImportTask(IServiceScopeFactory scopes, JobsOptions options, ILogger<JobsImportTask> log) : IScheduledTask
{
    public bool Enabled => options.WorkerEnabled;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        IReadOnlyList<JobSource> sources;
        using (var scope = scopes.CreateScope())
        {
            var importer = scope.ServiceProvider.GetRequiredService<JobImporter>();
            sources = run.Manual ? await importer.EnabledSourcesAsync(ct) : await importer.DueSourcesAsync(ct);
        }

        if (sources.Count == 0)
        {
            return "Aucune source à importer.";
        }

        // Une source en erreur ne bloque jamais les autres
        var (added, failed) = (0, new List<string>());
        foreach (var source in sources)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var report = await scope.ServiceProvider.GetRequiredService<JobImporter>().RunAsync(source, ct);
                added += report.Added;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Emploi : import {Source} en échec", source);
                failed.Add(source.ToString());
            }
        }

        var summary = $"{sources.Count} source(s), {added} offre(s) ajoutée(s)";
        if (failed.Count == sources.Count)
        {
            throw new InvalidOperationException($"Tous les imports ont échoué : {string.Join(", ", failed)}.");
        }

        return failed.Count == 0 ? summary + "." : $"{summary}, en échec : {string.Join(", ", failed)}.";
    }
}
