namespace BeninConnect.App.Features.Events.Models;

/// <summary>Événement communautaire (nommé ainsi pour éviter toute confusion avec le mot-clé event).</summary>
public sealed record CommunityEvent
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string City { get; init; }
    public string Country { get; init; } = string.Empty;
    public required string Address { get; init; }
    public required DateTimeOffset StartsAt { get; init; }
    public required DateTimeOffset EndsAt { get; init; }
    public required Guid OrganizerId { get; init; }
    public Guid? CommunityId { get; init; }
    public string? ImagePath { get; init; }
    public int? Capacity { get; init; }
    public IReadOnlySet<Guid> ParticipantIds { get; init; } = new HashSet<Guid>();

    public int ParticipantCount => ParticipantIds.Count;

    public bool IsFull => Capacity is { } capacity && ParticipantCount >= capacity;

    public bool IsParticipating(Guid userId) => ParticipantIds.Contains(userId);
}

public enum EventPeriod
{
    All,
    ThisWeek,
    ThisMonth,
    Later
}

public sealed record EventFilter(
    string? Search = null,
    string? Category = null,
    string? City = null,
    EventPeriod Period = EventPeriod.All);

public sealed record CreateEventRequest(
    Guid OrganizerId,
    string Title,
    string Description,
    string Category,
    string City,
    string Country,
    string Address,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int? Capacity,
    Guid? CommunityId,
    string? ImagePath);

public sealed record EventChangedMessage(CommunityEvent Event);

public sealed record EventCreatedMessage(CommunityEvent Event);
