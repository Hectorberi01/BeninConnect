using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Services.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class ServiceReviewRemindersJob : IBackgroundJob
{
    public string Key => ServicesTasks.ReviewReminders;
    public string Name => "Rappels d'avis";
    public string Module => "Services";
    public string Description => "Rappelle aux demandeurs de laisser un avis après une prestation terminée (Services:ReviewReminderHours).";
    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromHours(1), initialDelay: TimeSpan.FromMinutes(3));
}
