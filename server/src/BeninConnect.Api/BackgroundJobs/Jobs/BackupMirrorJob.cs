using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Administration.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class BackupMirrorJob(IConfiguration config) : IBackgroundJob
{
    public string Key => AdministrationTasks.BackupMirror;
    public string Name => "Copie des sauvegardes hors du serveur";
    public string Module => "Administration";
    public string Description => "Envoie les sauvegardes nouvelles ou modifiées vers le bucket S3 / R2 et supprime les copies trop anciennes (Backups:Offsite).";
    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(Math.Max(5, config.GetValue("Backups:Offsite:IntervalMinutes", 60))), initialDelay: TimeSpan.FromMinutes(5));
}
