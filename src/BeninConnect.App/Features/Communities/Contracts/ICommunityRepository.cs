using BeninConnect.App.Features.Communities.Models;

namespace BeninConnect.App.Features.Communities.Contracts;

public interface ICommunityRepository
{
    Task<IReadOnlyList<Community>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Community?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, Community>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Community>> GetForMemberAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Community>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityMember>> GetMembersAsync(Guid communityId, CancellationToken cancellationToken = default);

    /// <summary>Identifiants des communautés privées pour lesquelles l'utilisateur a une demande en attente.</summary>
    Task<IReadOnlySet<Guid>> GetPendingRequestsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Community> CreateAsync(CreateCommunityRequest request, CancellationToken cancellationToken = default);

    /// <summary>Communauté publique : adhésion immédiate. Privée : demande en attente.</summary>
    Task<MembershipStatus> JoinAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default);

    Task LeaveAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default);

    Task CancelJoinRequestAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default);
}
