using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Events.Models;
using BeninConnect.App.MockData.Seed;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.MockData.Repositories;

public sealed class MockEventRepository(MockDataStore store, IMessenger messenger, DeveloperOptions developer, TimeProvider clock)
    : MockRepositoryBase(store, messenger, developer, clock), IEventRepository
{
    public async Task<IReadOnlyList<CommunityEvent>> GetUpcomingAsync(EventFilter filter, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        var now = Now;
        var endOfWeek = now.Date.AddDays(7);
        var endOfMonth = now.Date.AddDays(31);

        lock (Store.SyncRoot)
        {
            return Store.Events
                .Where(e => e.EndsAt >= now)
                .Where(e => string.IsNullOrWhiteSpace(filter.Search)
                            || Matches(e.Title, filter.Search!)
                            || Matches(e.Description, filter.Search!)
                            || Matches(e.City, filter.Search!))
                .Where(e => filter.Category is null || e.Category == filter.Category)
                .Where(e => filter.City is null || e.City == filter.City)
                .Where(e => filter.Period switch
                {
                    EventPeriod.ThisWeek => e.StartsAt.Date < endOfWeek,
                    EventPeriod.ThisMonth => e.StartsAt.Date < endOfMonth,
                    EventPeriod.Later => e.StartsAt.Date >= endOfMonth,
                    _ => true
                })
                .OrderBy(e => e.StartsAt)
                .ToList();
        }
    }

    public async Task<CommunityEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Events.FirstOrDefault(e => e.Id == id);
        }
    }

    public async Task<IReadOnlyList<CommunityEvent>> GetForCommunityAsync(Guid communityId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        var now = Now;
        lock (Store.SyncRoot)
        {
            return Store.Events.Where(e => e.CommunityId == communityId && e.EndsAt >= now).OrderBy(e => e.StartsAt).ToList();
        }
    }

    public async Task<IReadOnlyList<CommunityEvent>> GetForParticipantAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        var now = Now;
        lock (Store.SyncRoot)
        {
            return Store.Events.Where(e => e.ParticipantIds.Contains(userId) && e.EndsAt >= now).OrderBy(e => e.StartsAt).ToList();
        }
    }

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(SeedEvents.Categories);

    public Task<IReadOnlyList<string>> GetCitiesAsync(CancellationToken cancellationToken = default)
    {
        lock (Store.SyncRoot)
        {
            IReadOnlyList<string> cities = Store.Events.Select(e => e.City).Distinct().OrderBy(c => c).ToList();
            return Task.FromResult(cities);
        }
    }

    public async Task<CommunityEvent> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);

        if (request.EndsAt <= request.StartsAt)
        {
            throw new DomainException("La fin de l'événement doit être postérieure à son début.");
        }

        var created = new CommunityEvent
        {
            Id = NewId(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category,
            City = request.City.Trim(),
            Country = request.Country.Trim(),
            Address = request.Address.Trim(),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            OrganizerId = request.OrganizerId,
            CommunityId = request.CommunityId,
            ImagePath = request.ImagePath ?? "Events/event_01.jpg",
            Capacity = request.Capacity,
            ParticipantIds = new HashSet<Guid> { request.OrganizerId }
        };

        lock (Store.SyncRoot)
        {
            Store.Events.Add(created);
        }

        Messenger.Send(new EventCreatedMessage(created));
        return created;
    }

    public async Task<bool> ToggleParticipationAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);

        CommunityEvent updated;
        bool participating;
        lock (Store.SyncRoot)
        {
            var current = Store.Events.FirstOrDefault(e => e.Id == eventId) ?? throw new NotFoundException("Événement");
            participating = !current.IsParticipating(userId);

            if (participating && current.IsFull)
            {
                throw new DomainException("Cet événement est complet.");
            }

            var participants = new HashSet<Guid>(current.ParticipantIds);
            if (participating)
            {
                participants.Add(userId);
            }
            else
            {
                participants.Remove(userId);
            }

            updated = MockDataStore.Replace(Store.Events, e => e.Id == eventId, e => e with { ParticipantIds = participants }, "Événement");
        }

        Messenger.Send(new EventChangedMessage(updated));
        return participating;
    }
}
