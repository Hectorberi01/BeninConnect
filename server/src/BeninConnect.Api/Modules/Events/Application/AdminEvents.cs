using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Events.Application;

public sealed record AdminEventDto(
    Guid Id, string Title, string Category, string City, DateTimeOffset StartsAt, string OrganizerName, string? CommunityName,
    int Participants, int? Capacity);

public sealed record GetAdminEventsQuery(string? Search, bool IncludePast = false) : IQuery<IReadOnlyList<AdminEventDto>>;

public sealed record DeleteEventCommand(Guid Id) : ICommand<bool>;

public sealed class AdminEventHandlers(IEventsDb db, IIdentityModule identity, ICommunitiesModule communities, IAdminSession admin, TimeProvider clock) :
    IRequestHandler<GetAdminEventsQuery, IReadOnlyList<AdminEventDto>>,
    IRequestHandler<DeleteEventCommand, bool>
{
    public async Task<IReadOnlyList<AdminEventDto>> Handle(GetAdminEventsQuery request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var events = db.Events.AsNoTracking();
        if (!request.IncludePast)
        {
            events = events.Where(e => e.EndsAt >= now);
        }

        if (Text.Like(request.Search) is { } q)
        {
            events = events.Where(e => e.Title.ToLower().Contains(q) || e.City.ToLower().Contains(q));
        }

        var list = await events.OrderBy(e => e.StartsAt)
            .Select(e => new { e.Id, e.Title, e.Category, e.City, e.StartsAt, e.OrganizerId, e.CommunityId, Participants = e.Participants.Count, e.Capacity })
            .ToListAsync(ct);
        var organizers = await identity.GetNamesAsync(list.Select(e => e.OrganizerId), ct);
        var groups = await communities.GetInfosAsync(list.Where(e => e.CommunityId != null).Select(e => e.CommunityId!.Value), ct);
        return list.Select(e => new AdminEventDto(e.Id, e.Title, e.Category, e.City, e.StartsAt, organizers.GetValueOrDefault(e.OrganizerId) ?? "—",
            e.CommunityId is { } c ? groups.GetValueOrDefault(c)?.Name : null, e.Participants, e.Capacity)).ToList();
    }

    public async Task<bool> Handle(DeleteEventCommand request, CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == request.Id, ct) ?? throw new NotFoundException("Événement");
        db.Events.Remove(ev);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Événement supprimé", "Event", ev.Id, ev.Title, ct);
        return true;
    }
}

public sealed class DetachEventsFromDeletedCommunity(IEventsDb db) : IIntegrationEventHandler<CommunityDeleted>
{
    public async Task Handle(CommunityDeleted e, CancellationToken ct)
    {
        foreach (var ev in await db.Events.Where(x => x.CommunityId == e.CommunityId).ToListAsync(ct))
        {
            ev.DetachFromCommunity();
        }

        await db.SaveChangesAsync(ct);
    }
}
