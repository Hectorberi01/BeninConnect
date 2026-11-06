using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Notifications.Models;
using BeninConnect.App.MockData.Seed;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.MockData.Repositories;

public sealed class MockCommunityRepository(MockDataStore store, IMessenger messenger, DeveloperOptions developer, TimeProvider clock)
    : MockRepositoryBase(store, messenger, developer, clock), ICommunityRepository
{
    public async Task<IReadOnlyList<Community>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Communities.OrderByDescending(c => c.MemberCount).ToList();
        }
    }

    public async Task<Community?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Communities.FirstOrDefault(c => c.Id == id);
        }
    }

    public Task<IReadOnlyDictionary<Guid, Community>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var set = ids.ToHashSet();
        lock (Store.SyncRoot)
        {
            IReadOnlyDictionary<Guid, Community> result = Store.Communities.Where(c => set.Contains(c.Id)).ToDictionary(c => c.Id);
            return Task.FromResult(result);
        }
    }

    public async Task<IReadOnlyList<Community>> GetForMemberAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Communities.Where(c => c.MemberIds.Contains(userId)).OrderBy(c => c.Name).ToList();
        }
    }

    public async Task<IReadOnlyList<Community>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        var q = query.Trim();
        lock (Store.SyncRoot)
        {
            return Store.Communities
                .Where(c => q.Length == 0
                            || Matches(c.Name, q)
                            || Matches(c.Description, q)
                            || Matches(c.City, q)
                            || Matches(c.Country, q)
                            || Matches(c.Category, q))
                .OrderByDescending(c => c.MemberCount)
                .ToList();
        }
    }

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(SeedCommunities.Categories);

    public async Task<IReadOnlyList<CommunityMember>> GetMembersAsync(Guid communityId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            var community = Store.Communities.FirstOrDefault(c => c.Id == communityId)
                            ?? throw new NotFoundException("Communauté");

            return Store.Users
                .Where(u => community.MemberIds.Contains(u.Id))
                .Select(u => new CommunityMember(u, RoleOf(community, u.Id)))
                .OrderByDescending(m => m.Role)
                .ThenBy(m => m.User.FirstName)
                .ToList();
        }
    }

    public async Task<IReadOnlySet<Guid>> GetPendingRequestsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.JoinRequests.Where(r => r.UserId == userId).Select(r => r.CommunityId).ToHashSet();
        }
    }

    public async Task<Community> CreateAsync(CreateCommunityRequest request, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);

        Community community;
        lock (Store.SyncRoot)
        {
            if (Store.Communities.Any(c => string.Equals(c.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new DomainException("Une communauté porte déjà ce nom.");
            }

            community = new Community
            {
                Id = NewId(),
                Name = request.Name.Trim(),
                Description = request.Description.Trim(),
                Category = request.Category,
                Country = request.Country.Trim(),
                City = request.City.Trim(),
                Visibility = request.Visibility,
                CoverImage = request.CoverImage ?? "Covers/community_01.jpg",
                OwnerId = request.OwnerId,
                CreatedAt = Now,
                MemberIds = new HashSet<Guid> { request.OwnerId }
            };
            Store.Communities.Add(community);
        }

        Messenger.Send(new CommunityCreatedMessage(community));
        Messenger.Send(new MembershipChangedMessage(community.Id, request.OwnerId, MembershipStatus.Owner));
        return community;
    }

    public async Task<MembershipStatus> JoinAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);

        Community community;
        MembershipStatus status;
        lock (Store.SyncRoot)
        {
            community = Store.Communities.FirstOrDefault(c => c.Id == communityId) ?? throw new NotFoundException("Communauté");

            if (community.MemberIds.Contains(userId))
            {
                return MembershipStatus.Member;
            }

            if (community.IsPrivate)
            {
                if (!Store.JoinRequests.Any(r => r.CommunityId == communityId && r.UserId == userId))
                {
                    Store.JoinRequests.Add(new JoinRequest(NewId(), communityId, userId, Now));
                }

                status = MembershipStatus.Pending;
            }
            else
            {
                community = MockDataStore.Replace(Store.Communities, c => c.Id == communityId,
                    c => c with { MemberIds = new HashSet<Guid>(c.MemberIds) { userId } }, "Communauté");
                status = MembershipStatus.Member;

                Store.Notifications.Add(new Notification
                {
                    Id = NewId(),
                    UserId = userId,
                    Category = NotificationCategory.Communities,
                    Title = $"Bienvenue dans {community.Name}",
                    Body = "Présentez-vous aux membres et découvrez les prochains événements.",
                    CreatedAt = Now,
                    Target = NotificationTarget.Community,
                    TargetId = community.Id
                });
            }
        }

        Messenger.Send(new CommunityChangedMessage(community));
        Messenger.Send(new MembershipChangedMessage(communityId, userId, status));
        if (status == MembershipStatus.Member)
        {
            Messenger.Send(new NotificationsChangedMessage(userId));
        }

        return status;
    }

    public async Task LeaveAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);

        Community community;
        lock (Store.SyncRoot)
        {
            community = Store.Communities.FirstOrDefault(c => c.Id == communityId) ?? throw new NotFoundException("Communauté");
            if (community.OwnerId == userId)
            {
                throw new DomainException("Le créateur ne peut pas quitter sa communauté.");
            }

            community = MockDataStore.Replace(Store.Communities, c => c.Id == communityId, c =>
            {
                var members = new HashSet<Guid>(c.MemberIds);
                members.Remove(userId);
                return c with { MemberIds = members };
            }, "Communauté");
        }

        Messenger.Send(new CommunityChangedMessage(community));
        Messenger.Send(new MembershipChangedMessage(communityId, userId, MembershipStatus.None));
    }

    public async Task CancelJoinRequestAsync(Guid communityId, Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        Community? community;
        lock (Store.SyncRoot)
        {
            Store.JoinRequests.RemoveAll(r => r.CommunityId == communityId && r.UserId == userId);
            community = Store.Communities.FirstOrDefault(c => c.Id == communityId);
        }

        if (community is not null)
        {
            Messenger.Send(new CommunityChangedMessage(community));
        }

        Messenger.Send(new MembershipChangedMessage(communityId, userId, MembershipStatus.None));
    }

    private static CommunityRole RoleOf(Community community, Guid userId) =>
        community.OwnerId == userId ? CommunityRole.Owner
        : community.ModeratorIds.Contains(userId) ? CommunityRole.Moderator
        : CommunityRole.Member;
}
