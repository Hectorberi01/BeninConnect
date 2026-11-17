using BeninConnect.Modules.Notifications.Application;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Notifications.Infrastructure;

/// <summary>Section « Notifications:Retention » (en jours).</summary>
/// <summary>Section « Notifications:Broadcast ».</summary>
public sealed class BroadcastSettings
{
    public const string Section = "Notifications:Broadcast";

    /// <summary>Membres notifiés par passage (toutes les minutes).</summary>
    public int WaveSize { get; set; } = 1000;
}

/// <summary>Notifications à tous les membres (publications du compte officiel), par vagues.</summary>
public sealed class BroadcastsTask(Broadcasts broadcasts, BroadcastSettings settings) : IScheduledTask
{
    public bool Enabled => true;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct) =>
        $"{await broadcasts.SendWaveAsync(Math.Max(1, settings.WaveSize), ct)} membre(s) notifié(s).";
}

public sealed class NotificationRetention
{
    public const string Section = "Notifications:Retention";

    public int ReadDays { get; set; } = 90;
    public int AllDays { get; set; } = 180;

    /// <summary>Appareil push sans activité depuis ce délai (application désinstallée, jeton périmé).</summary>
    public int DeviceDays { get; set; } = 180;
}

/// <summary>Purge : notifications lues anciennes, toutes les notifications très anciennes, appareils push inactifs.</summary>
public sealed class NotificationsPurgeTask(INotificationsDb db, NotificationRetention retention, TimeProvider clock) : IScheduledTask
{
    public bool Enabled => true;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var readLimit = now.AddDays(-Math.Max(1, retention.ReadDays));
        var allLimit = now.AddDays(-Math.Max(retention.ReadDays, retention.AllDays));
        var deviceLimit = now.AddDays(-Math.Max(1, retention.DeviceDays));

        var notifications = await db.Notifications
            .Where(n => n.CreatedAt < allLimit || (n.IsRead && n.CreatedAt < readLimit))
            .ExecuteDeleteAsync(ct);
        var devices = await db.PushDevices.Where(d => d.LastSeenAt < deviceLimit).ExecuteDeleteAsync(ct);
        return $"{notifications} notification(s) et {devices} appareil(s) inactif(s) supprimé(s).";
    }
}
