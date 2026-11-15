using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Administration.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class DailyBackupJob : IBackgroundJob
{
    public string Key => AdministrationTasks.DailyBackup;
    public string Name => "Sauvegarde quotidienne de la base";
    public string Module => "Administration";
    public string Description => "pg_dump de la base après Backups:DailyAtUtc (une fois par jour, rattrapée si l'API redémarre), rotation, puis copie hors du serveur. Lancée à la main : sauvegarde immédiate.";
    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(10), initialDelay: TimeSpan.FromMinutes(2));
}
