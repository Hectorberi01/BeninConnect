using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.Identity.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class AccountsCleanupJob : IBackgroundJob
{
    public string Key => IdentityTasks.AccountsCleanup;
    public string Name => "Nettoyage des comptes";
    public string Module => "Identité";
    public string Description => "Efface les codes de réinitialisation expirés et supprime les comptes jamais vérifiés après 30 jours (Identity:Cleanup:UnverifiedAccountDays), avec toutes leurs données.";
    public JobSchedule Schedule { get; } = JobSchedule.DailyAt(new TimeOnly(3, 30));
}
