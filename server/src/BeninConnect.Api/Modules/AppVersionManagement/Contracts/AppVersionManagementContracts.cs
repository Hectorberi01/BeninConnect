namespace BeninConnect.Modules.AppVersionManagement.Contracts;

/// <summary>Clés des tâches de fond du module (planifiées par BackgroundJobs).</summary>
public static class AppVersionTasks
{
    public const string DetectOutdated = "app-versions.detect-outdated";
    public const string SendCampaigns = "app-versions.send-campaigns";
}
