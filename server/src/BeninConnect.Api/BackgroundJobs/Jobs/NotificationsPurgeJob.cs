using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Notifications.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class NotificationsPurgeJob : IBackgroundJob
{
    public string Key => NotificationsTasks.Purge;
    public string Name => "Purge des notifications";
    public string Module => "Notifications";
    public string Description => "Supprime les notifications lues de plus de 90 jours, toutes celles de plus de 180 jours et les appareils push inactifs depuis 180 jours (Notifications:Retention).";
    public JobSchedule Schedule { get; } = JobSchedule.DailyAt(new TimeOnly(3, 20));
}
