namespace BeninConnect.App.Features.Notifications.Models;

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

public sealed record Notification
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required NotificationCategory Category { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public bool IsRead { get; init; }
    public NotificationTarget Target { get; init; }
    public Guid? TargetId { get; init; }
}

public sealed record NotificationsChangedMessage(Guid UserId);
