using BeninConnect.BackgroundJobs.Scheduling;
using BeninConnect.Modules.AppVersionManagement.Contracts;

namespace BeninConnect.BackgroundJobs.Jobs;

public sealed class UpdateCampaignSendJob : IBackgroundJob
{
    public string Key => AppVersionTasks.SendCampaigns;
    public string Name => "Envoi des relances de mise à jour";
    public string Module => "Versions de l'app";
    public string Description => "Envoie par lots les relances validées par un administrateur ; les membres passés à jour entre-temps ne sont pas relancés.";
    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(2));
}
