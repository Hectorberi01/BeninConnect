using BeninConnect.BackgroundJobs.Scheduling;

namespace BeninConnect.BackgroundJobs.Jobs;

/// <summary>Environnement de revue Apple / Google seulement (tâche enregistrée par Program.cs).</summary>
public sealed class ReviewResetJob(IConfiguration config) : IBackgroundJob
{
    public const string Key = "review.reset";

    string IBackgroundJob.Key => Key;
    public string Name => "Remise à zéro de la revue";
    public string Module => "Revue";
    public string Description => "Efface les contenus créés par les testeurs Apple / Google et recharge le jeu de démonstration (Review:ResetDailyAtUtc).";
    public JobSchedule Schedule { get; } =
        JobSchedule.DailyAt(TimeOnly.TryParse(config["Review:ResetDailyAtUtc"], out var at) ? at : new TimeOnly(3, 0));
}
