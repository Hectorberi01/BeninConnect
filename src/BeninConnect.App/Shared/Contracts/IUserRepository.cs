using BeninConnect.App.Shared.Models;

namespace BeninConnect.App.Shared.Contracts;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<User> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>Centres d'intérêt proposés dans le profil.</summary>
    IReadOnlyList<string> AvailableInterests { get; }
}
