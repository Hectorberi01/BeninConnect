using BeninConnect.Modules.Editorial.Application;
using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Editorial.Infrastructure;

/// <summary>Publication des contenus validés dont l'heure est venue. Active seulement si Editorial:Enabled.</summary>
public sealed class EditorialPublishTask(EditorialPipeline pipeline, EditorialSettings settings) : IScheduledTask
{
    public bool Enabled => settings.Enabled;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var (published, failed) = await pipeline.PublishDueAsync(ct);
        return $"{published} publié(s), {failed} en échec.";
    }
}

/// <summary>Collecte des sources (flux RSS / Atom).</summary>
public sealed class EditorialCollectTask(EditorialPipeline pipeline, EditorialSettings settings) : IScheduledTask
{
    public bool Enabled => settings.Enabled;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct) =>
        $"{await pipeline.CollectAsync(ct)} contenu(s) collecté(s).";
}

/// <summary>Rédaction des brouillons par le LLM.</summary>
public sealed class EditorialDraftTask(EditorialPipeline pipeline, EditorialSettings settings) : IScheduledTask
{
    public bool Enabled => settings.Enabled;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var (drafted, skipped) = await pipeline.DraftAsync(ct);
        return $"{drafted} brouillon(s) rédigé(s), {skipped} ignoré(s).";
    }
}
