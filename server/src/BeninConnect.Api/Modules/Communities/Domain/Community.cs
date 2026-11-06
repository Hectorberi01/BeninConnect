using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Communities.Domain;

public enum CommunityVisibility
{
    Public,
    Private
}

public enum CommunityRole
{
    Member,
    Moderator,
    Owner
}

public enum MembershipStatus
{
    None,
    Pending,
    Member,
    Owner
}

public sealed class Community
{
    private readonly List<CommunityMember> _members = [];
    private readonly List<JoinRequest> _joinRequests = [];

    private Community()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public CommunityVisibility Visibility { get; private set; }
    public string? CoverImage { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<CommunityMember> Members => _members;
    public IReadOnlyCollection<JoinRequest> JoinRequests => _joinRequests;

    public bool IsPrivate => Visibility == CommunityVisibility.Private;

    public static Community Create(
        Guid id, string name, string description, string category, string country, string city,
        CommunityVisibility visibility, string? coverImage, Guid ownerId, DateTimeOffset now)
    {
        var community = new Community
        {
            Id = id,
            Name = name.Trim(),
            Description = description.Trim(),
            Category = category,
            Country = country.Trim(),
            City = city.Trim(),
            Visibility = visibility,
            CoverImage = coverImage,
            OwnerId = ownerId,
            CreatedAt = now
        };
        community._members.Add(new CommunityMember(id, ownerId, CommunityRole.Owner, now));
        return community;
    }

    public bool IsMember(Guid userId) => _members.Any(m => m.UserId == userId);

    public MembershipStatus StatusFor(Guid userId) =>
        OwnerId == userId ? MembershipStatus.Owner
        : IsMember(userId) ? MembershipStatus.Member
        : _joinRequests.Any(r => r.UserId == userId) ? MembershipStatus.Pending
        : MembershipStatus.None;

    /// <summary>Publique : adhésion immédiate. Privée : demande en attente.</summary>
    public MembershipStatus Join(Guid userId, DateTimeOffset now)
    {
        if (IsMember(userId))
        {
            return StatusFor(userId);
        }

        if (IsPrivate)
        {
            if (_joinRequests.All(r => r.UserId != userId))
            {
                _joinRequests.Add(new JoinRequest(Guid.NewGuid(), Id, userId, now));
            }

            return MembershipStatus.Pending;
        }

        _members.Add(new CommunityMember(Id, userId, CommunityRole.Member, now));
        return MembershipStatus.Member;
    }

    /// <summary>Ajout direct (jeu de démonstration, approbation d'une demande).</summary>
    public void AddMember(Guid userId, CommunityRole role, DateTimeOffset now)
    {
        if (!IsMember(userId))
        {
            _members.Add(new CommunityMember(Id, userId, role, now));
        }

        _joinRequests.RemoveAll(r => r.UserId == userId);
    }

    public void AddJoinRequest(Guid userId, DateTimeOffset now)
    {
        if (!IsMember(userId) && _joinRequests.All(r => r.UserId != userId))
        {
            _joinRequests.Add(new JoinRequest(Guid.NewGuid(), Id, userId, now));
        }
    }

    public void Leave(Guid userId)
    {
        if (OwnerId == userId)
        {
            throw new DomainException("Le créateur ne peut pas quitter sa communauté.");
        }

        _members.RemoveAll(m => m.UserId == userId);
    }

    public void CancelJoinRequest(Guid userId) => _joinRequests.RemoveAll(r => r.UserId == userId);
}

public sealed class CommunityMember
{
    private CommunityMember()
    {
    }

    public CommunityMember(Guid communityId, Guid userId, CommunityRole role, DateTimeOffset joinedAt)
    {
        CommunityId = communityId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid CommunityId { get; private set; }
    public Guid UserId { get; private set; }
    public CommunityRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
}

public sealed class JoinRequest
{
    private JoinRequest()
    {
    }

    public JoinRequest(Guid id, Guid communityId, Guid userId, DateTimeOffset requestedAt)
    {
        Id = id;
        CommunityId = communityId;
        UserId = userId;
        RequestedAt = requestedAt;
    }

    public Guid Id { get; private set; }
    public Guid CommunityId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
}
