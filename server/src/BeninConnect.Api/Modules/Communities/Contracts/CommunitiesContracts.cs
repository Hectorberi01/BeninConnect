using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Communities.Contracts;

public sealed record CommunityInfo(Guid Id, string Name, Guid OwnerId);

/// <summary>Adhésion d'un membre (console). Role : Member, Moderator, Owner.</summary>
public sealed record MemberCommunity(Guid Id, string Name, string Role, bool IsPrivate, DateTimeOffset JoinedAt);

public sealed record CommunityStats(int Total, int Private, IReadOnlyList<LabelCount> TopByMembers);

public sealed record CommunityExportRow(
    Guid Id, string Name, string Category, string Country, string City, string Visibility, int Members, DateTimeOffset CreatedAt);

public interface ICommunitiesModule
{
    Task<IReadOnlyList<Guid>> GetCommunityIdsOfMemberAsync(Guid userId, CancellationToken ct);

    Task<bool> IsMemberAsync(Guid communityId, Guid userId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, CommunityInfo>> GetInfosAsync(IEnumerable<Guid> ids, CancellationToken ct);

    Task<IReadOnlyList<string>> GetCommunityNamesOfMemberAsync(Guid userId, CancellationToken ct);

    /// <summary>Communautés d'un membre avec son rôle, de la plus récente à la plus ancienne.</summary>
    Task<IReadOnlyList<MemberCommunity>> GetMembershipsAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, int>> CountMembershipsByUserAsync(CancellationToken ct);

    /// <summary>Suppression par la modération ; retourne le nom. Publie CommunityDeleted.</summary>
    Task<string> DeleteAsync(Guid communityId, CancellationToken ct);

    Task<CommunityStats> GetStatsAsync(CancellationToken ct);

    Task<IReadOnlyList<CommunityExportRow>> GetExportRowsAsync(CancellationToken ct);
}

public sealed record CommunityJoined(Guid CommunityId, string CommunityName, Guid UserId) : IIntegrationEvent;

/// <summary>Les publications et événements rattachés deviennent hors communauté.</summary>
public sealed record CommunityDeleted(Guid CommunityId) : IIntegrationEvent;
