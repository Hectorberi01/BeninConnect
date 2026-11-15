using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Editorial.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class EditorialCollectJob(IConfiguration config) : IBackgroundJob
{
    public string Key => EditorialTasks.Collect;
    public string Name => "Collecte des sources éditoriales";
    public string Module => "Éditorial";
    public string Description => "Lit les flux RSS / Atom des sources activées et enregistre les nouveaux contenus. Actif seulement si Editorial:Enabled.";
    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Editorial:CollectEveryMinutes", 60))));
}
