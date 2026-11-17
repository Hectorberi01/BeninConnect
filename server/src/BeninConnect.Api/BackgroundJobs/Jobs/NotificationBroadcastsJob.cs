using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Notifications.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class NotificationBroadcastsJob : IBackgroundJob
{
    public string Key => NotificationsTasks.Broadcasts;
    public string Name => "Notifications à tous les membres";
    public string Module => "Notifications";
    public string Description => "Publications du compte officiel : notifie les membres par vagues (Notifications:Broadcast:WaveSize, 1 000 par minute). Un seul push par jour, les suivantes restent visibles dans l'app.";
    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(1));
}
