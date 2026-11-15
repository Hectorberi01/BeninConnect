using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Administration.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class BackupVerifyJob : IBackgroundJob
{
    public string Key => AdministrationTasks.BackupVerify;
    public string Name => "Vérification des sauvegardes";
    public string Module => "Administration";
    public string Description => "Dernière sauvegarde de la base : moins de 48 h, archive lisible jusqu'au bout, dump complet, copie distante identique. En échec si un contrôle ne passe pas.";
    public JobSchedule Schedule { get; } = JobSchedule.WeeklyAt(DayOfWeek.Monday, new TimeOnly(4, 10));
}
