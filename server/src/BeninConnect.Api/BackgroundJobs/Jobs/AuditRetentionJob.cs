using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Administration.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class AuditRetentionJob : IBackgroundJob
{
    public string Key => AdministrationTasks.AuditRetention;
    public string Name => "Rétention du journal d'audit";
    public string Module => "Administration";
    public string Description => "Supprime les entrées du journal d'audit plus anciennes que Audit:RetentionMonths (12 mois par défaut).";
    public JobSchedule Schedule { get; } = JobSchedule.DailyAt(new TimeOnly(3, 40));
}
