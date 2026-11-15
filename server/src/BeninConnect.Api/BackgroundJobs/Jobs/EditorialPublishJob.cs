using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Editorial.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class EditorialPublishJob(IConfiguration config) : IBackgroundJob
{
    public string Key => EditorialTasks.Publish;
    public string Name => "Publication des contenus validés";
    public string Module => "Éditorial";
    public string Description => "Publie les contenus validés dont l'heure de publication est passée. Actif seulement si Editorial:Enabled.";
    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Editorial:PublishEveryMinutes", 5))));
}
