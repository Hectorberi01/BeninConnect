using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Jobs.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class JobOffersImportJob : IBackgroundJob
{
    public string Key => JobsTasks.Import;
    public string Name => "Import des offres d'emploi";
    public string Module => "Emploi";
    public string Description => "Lance les sources activées dont l'intervalle d'import est écoulé (réglé par source dans la page Emploi). Désactivé par Jobs:WorkerEnabled.";
    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(1));
}
