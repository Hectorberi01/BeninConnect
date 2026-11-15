using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Editorial.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class EditorialDraftJob(IConfiguration config) : IBackgroundJob
{
    public string Key => EditorialTasks.Draft;
    public string Name => "Rédaction des brouillons";
    public string Module => "Éditorial";
    public string Description => "Fait rédiger par le LLM les brouillons des contenus collectés. Actif seulement si Editorial:Enabled.";
    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Editorial:DraftEveryMinutes", 15))));
}
