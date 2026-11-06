using BeninConnect.App.Shared.Models;

namespace BeninConnect.App.Features.Communities.Models;

public enum CommunityVisibility
{
    Public,
    Private
}

public enum MembershipStatus
{
    None,
    Pending,
    Member,
    Owner
}

public enum CommunityRole
{
    Member,
    Moderator,
    Owner
}

public sealed record Community
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public string Country { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public CommunityVisibility Visibility { get; init; } = CommunityVisibility.Public;
    public string? CoverImage { get; init; }
    public required Guid OwnerId { get; init; }
    public IReadOnlySet<Guid> ModeratorIds { get; init; } = new HashSet<Guid>();
    public required DateTimeOffset CreatedAt { get; init; }
    public IReadOnlySet<Guid> MemberIds { get; init; } = new HashSet<Guid>();

    public int MemberCount => MemberIds.Count;

    public bool IsPrivate => Visibility == CommunityVisibility.Private;

    public string Location =>
        string.Join(", ", new[] { City, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public MembershipStatus StatusFor(Guid userId, IReadOnlySet<Guid> pendingCommunityIds)
    {
        if (OwnerId == userId)
        {
            return MembershipStatus.Owner;
        }

        if (MemberIds.Contains(userId))
        {
            return MembershipStatus.Member;
        }

        return pendingCommunityIds.Contains(Id) ? MembershipStatus.Pending : MembershipStatus.None;
    }
}

public sealed record CommunityMember(User User, CommunityRole Role);

public sealed record JoinRequest(Guid Id, Guid CommunityId, Guid UserId, DateTimeOffset RequestedAt);

public sealed record CreateCommunityRequest(
    Guid OwnerId,
    string Name,
    string Description,
    string Category,
    string Country,
    string City,
    CommunityVisibility Visibility,
    string? CoverImage);

public sealed record CommunityChangedMessage(Community Community);

public sealed record CommunityCreatedMessage(Community Community);

/// <summary>Adhésion modifiée (rejoindre, quitter, demande envoyée ou annulée).</summary>
public sealed record MembershipChangedMessage(Guid CommunityId, Guid UserId, MembershipStatus Status);
