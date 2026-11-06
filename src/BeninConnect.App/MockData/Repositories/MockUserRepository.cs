using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.MockData.Seed;
using BeninConnect.App.Shared.Contracts;
using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.MockData.Repositories;

public sealed class MockUserRepository(MockDataStore store, IMessenger messenger, DeveloperOptions developer, TimeProvider clock)
    : MockRepositoryBase(store, messenger, developer, clock), IUserRepository
{
    public IReadOnlyList<string> AvailableInterests => SeedUsers.Interests;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Users.FirstOrDefault(u => u.Id == id);
        }
    }

    public Task<IReadOnlyDictionary<Guid, User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        // Lecture groupée « gratuite » : une API renverrait l'auteur embarqué dans chaque publication.
        cancellationToken.ThrowIfCancellationRequested();
        var set = ids.ToHashSet();
        lock (Store.SyncRoot)
        {
            IReadOnlyDictionary<Guid, User> result = Store.Users.Where(u => set.Contains(u.Id)).ToDictionary(u => u.Id);
            return Task.FromResult(result);
        }
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Users.OrderBy(u => u.FirstName).ToList();
        }
    }

    public async Task<IReadOnlyList<User>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        var q = query.Trim();
        lock (Store.SyncRoot)
        {
            return Store.Users
                .Where(u => q.Length == 0
                            || Matches(u.DisplayName, q)
                            || Matches(u.City, q)
                            || Matches(u.Country, q)
                            || Matches(u.Profession, q)
                            || u.Interests.Any(i => Matches(i, q)))
                .OrderBy(u => u.FirstName)
                .ToList();
        }
    }

    public async Task<User> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        User updated;
        lock (Store.SyncRoot)
        {
            updated = MockDataStore.Replace(
                Store.Users,
                u => u.Id == request.UserId,
                u => u with
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Profession = request.Profession.Trim(),
                    Country = request.Country.Trim(),
                    City = request.City.Trim(),
                    Bio = request.Bio.Trim(),
                    Interests = request.Interests.ToList()
                },
                "Utilisateur");
        }

        Messenger.Send(new UserUpdatedMessage(updated));
        return updated;
    }
}
