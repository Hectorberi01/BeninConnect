using BeninConnect.App.Features.Events.Models;

namespace BeninConnect.App.Features.Events.Contracts;

public interface IEventRepository
{
    Task<IReadOnlyList<CommunityEvent>> GetUpcomingAsync(EventFilter filter, CancellationToken cancellationToken = default);

    Task<CommunityEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityEvent>> GetForCommunityAsync(Guid communityId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityEvent>> GetForParticipantAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCitiesAsync(CancellationToken cancellationToken = default);

    Task<CommunityEvent> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken = default);

    /// <summary>Inscrit ou désinscrit. Retourne true si l'utilisateur est désormais inscrit.</summary>
    Task<bool> ToggleParticipationAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default);
}
