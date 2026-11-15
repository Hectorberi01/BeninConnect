namespace BeninConnect.BackgroundJobs.Scheduling;

/// <summary>Section « BackgroundJobs ».</summary>
public sealed class BackgroundJobsOptions
{
    public const string Section = "BackgroundJobs";

    /// <summary>false : aucune exécution planifiée (tests) ; le lancement manuel reste possible.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Clés des tâches à ne pas planifier (ex. BackgroundJobs__Disabled__0=media.orphans).</summary>
    public HashSet<string> Disabled { get; set; } = [];

    public int TickSeconds { get; set; } = 30;
}
