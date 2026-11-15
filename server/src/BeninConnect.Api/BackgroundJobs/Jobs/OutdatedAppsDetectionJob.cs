using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.AppVersionManagement.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class OutdatedAppsDetectionJob : IBackgroundJob
{
    public string Key => AppVersionTasks.DetectOutdated;
    public string Name => "Détection des applications non à jour";
    public string Module => "Versions de l'app";
    public string Description => "Repère les membres dont l'application n'est pas à jour et prépare une relance (push + email) à valider dans la page Versions de l'app. Rien n'est envoyé sans validation.";
    public JobSchedule Schedule { get; } = JobSchedule.DailyAt(new TimeOnly(8, 40));
}
