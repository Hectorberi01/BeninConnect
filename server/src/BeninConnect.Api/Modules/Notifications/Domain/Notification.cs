namespace BeninConnect.Modules.Notifications.Domain;

public enum NotificationCategory
{
    Social,
    Communities,
    Messages,
    Events,
    System
}

public enum NotificationTarget
{
    None,
    Post,
    Community,
    Conversation,
    Event,
    Profile
}

public sealed class Notification
{
    private Notification()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationCategory Category { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsRead { get; private set; }
    public NotificationTarget Target { get; private set; }
    public Guid? TargetId { get; private set; }

    public static Notification Create(
        Guid id, Guid userId, NotificationCategory category, string title, string body,
        DateTimeOffset now, NotificationTarget target = NotificationTarget.None, Guid? targetId = null, bool isRead = false) =>
        new()
        {
            Id = id,
            UserId = userId,
            Category = category,
            Title = title,
            Body = body,
            CreatedAt = now,
            Target = target,
            TargetId = targetId,
            IsRead = isRead
        };

    public void MarkRead() => IsRead = true;
}
