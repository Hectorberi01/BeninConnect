using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Events.Domain;

public enum EventPeriod
{
    All,
    ThisWeek,
    ThisMonth,
    Later
}

public sealed class CommunityEvent
{
    private readonly List<EventParticipant> _participants = [];

    private CommunityEvent()
    {
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public Guid OrganizerId { get; private set; }
    public Guid? CommunityId { get; private set; }

    /// <summary>Communauté supprimée : l'événement reste, hors communauté.</summary>
    public void DetachFromCommunity() => CommunityId = null;
    public string? ImagePath { get; private set; }
    public int? Capacity { get; private set; }

    public IReadOnlyCollection<EventParticipant> Participants => _participants;

    public bool IsFull => Capacity is { } capacity && _participants.Count >= capacity;

    public static CommunityEvent Create(
        Guid id, string title, string description, string category, string city, string country, string address,
        DateTimeOffset startsAt, DateTimeOffset endsAt, Guid organizerId, Guid? communityId, string? imagePath, int? capacity,
        DateTimeOffset now, bool allowPast = false)
    {
        if (endsAt <= startsAt)
        {
            throw new DomainException("La fin de l'événement doit être postérieure à son début.");
        }

        if (!allowPast && startsAt <= now)
        {
            throw new DomainException("L'événement doit avoir lieu dans le futur.");
        }

        if (capacity is < 2)
        {
            throw new DomainException("La capacité doit être d'au moins 2 places.");
        }

        var created = new CommunityEvent
        {
            Id = id,
            Title = title.Trim(),
            Description = description.Trim(),
            Category = category,
            City = city.Trim(),
            Country = country.Trim(),
            Address = address.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            OrganizerId = organizerId,
            CommunityId = communityId,
            ImagePath = imagePath,
            Capacity = capacity
        };
        created._participants.Add(new EventParticipant(id, organizerId, now));
        return created;
    }

    public bool IsParticipating(Guid userId) => _participants.Any(p => p.UserId == userId);

    /// <summary>Inscrit ou désinscrit ; retourne true si l'utilisateur est désormais inscrit.</summary>
    public bool ToggleParticipation(Guid userId, DateTimeOffset now)
    {
        var existing = _participants.FirstOrDefault(p => p.UserId == userId);
        if (existing is not null)
        {
            _participants.Remove(existing);
            return false;
        }

        if (IsFull)
        {
            throw new DomainException("Cet événement est complet.");
        }

        _participants.Add(new EventParticipant(Id, userId, now));
        return true;
    }
}

public sealed class EventParticipant
{
    private EventParticipant()
    {
    }

    public EventParticipant(Guid eventId, Guid userId, DateTimeOffset registeredAt)
    {
        EventId = eventId;
        UserId = userId;
        RegisteredAt = registeredAt;
    }

    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
}
