namespace BeninConnect.Modules.Notifications.Domain;

/// <summary>
/// Notification envoyée à tous les membres, distribuée par vagues (tâche notifications.broadcasts).
/// LastUserId : curseur, les membres sont parcourus par identifiant croissant.
/// </summary>
public sealed class Broadcast
{
    private Broadcast()
    {
    }

    public Guid Id { get; private set; }
    public NotificationCategory Category { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationTarget Target { get; private set; }
    public Guid? TargetId { get; private set; }

    /// <summary>false : notification dans l'app seulement (un push par jour au plus).</summary>
    public bool SendPush { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? LastUserId { get; private set; }
    public int Delivered { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static Broadcast Create(Guid id, NotificationCategory category, string title, string body, NotificationTarget target, Guid? targetId,
        bool sendPush, DateTimeOffset now) => new()
    {
        Id = id, Category = category, Title = title, Body = body, Target = target, TargetId = targetId, SendPush = sendPush, CreatedAt = now,
    };

    public void Advance(IReadOnlyList<Guid> userIds, bool done, DateTimeOffset now)
    {
        if (userIds.Count > 0)
        {
            LastUserId = userIds[^1];
            Delivered += userIds.Count;
        }

        if (done)
        {
            CompletedAt = now;
        }
    }
}
