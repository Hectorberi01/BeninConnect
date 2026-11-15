using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Voyages.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class TripLifecycleJob : IBackgroundJob
{
    public string Key => VoyagesTasks.TripLifecycle;
    public string Name => "Cycle de vie des trajets";
    public string Module => "Voyages";
    public string Description => "Rappels la veille du départ, trajets passés au statut parti, trajets des voyageurs suspendus masqués.";
    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromHours(1), initialDelay: TimeSpan.FromMinutes(4));
}
