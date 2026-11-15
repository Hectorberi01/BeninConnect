using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Media.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class OrphanMediaJob : IBackgroundJob
{
    public string Key => MediaTasks.Orphans;
    public string Name => "Fichiers orphelins";
    public string Module => "Médias";
    public string Description => "Recherche les photos et vidéos que plus aucun module n'utilise (plus de 7 jours). Rapport seulement, sauf si Media:OrphanCleanup:Delete=true.";
    public JobSchedule Schedule { get; } = JobSchedule.WeeklyAt(DayOfWeek.Sunday, new TimeOnly(4, 30));
}
