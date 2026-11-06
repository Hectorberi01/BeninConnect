using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Events.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Events.Application;

public interface IEventsDb
{
    DbSet<CommunityEvent> Events { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record EventDto(
    Guid Id, string Title, string Description, string Category, string City, string Country, string Address,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, Guid OrganizerId, Guid? CommunityId, string? ImagePath,
    int? Capacity, IReadOnlyList<Guid> ParticipantIds);

public sealed record ParticipationDto(bool IsParticipating, EventDto Event);

public static class EventsCatalog
{
    public static readonly string[] Categories = ["Culture", "Networking", "Gastronomie", "Sport", "Formation", "Entraide", "Famille"];

    /// <summary>Nécessite l'inclusion de Participants.</summary>
    public static EventDto ToDto(this CommunityEvent e) => new(
        e.Id, e.Title, e.Description, e.Category, e.City, e.Country, e.Address, e.StartsAt, e.EndsAt,
        e.OrganizerId, e.CommunityId, e.ImagePath, e.Capacity, e.Participants.Select(p => p.UserId).ToList());
}

public sealed record GetEventsQuery(string? Search, string? Category, string? City, EventPeriod Period) : IQuery<IReadOnlyList<EventDto>>;

public sealed record GetEventQuery(Guid Id) : IQuery<EventDto>;

public sealed record GetCommunityEventsQuery(Guid CommunityId) : IQuery<IReadOnlyList<EventDto>>;

public sealed record GetParticipantEventsQuery(Guid UserId) : IQuery<IReadOnlyList<EventDto>>;

public sealed record GetEventCategoriesQuery : IQuery<IReadOnlyList<string>>;

public sealed record GetEventCitiesQuery : IQuery<IReadOnlyList<string>>;

public sealed record CreateEventCommand(
    string Title, string Description, string Category, string City, string Country, string Address,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, int? Capacity, Guid? CommunityId, string? ImagePath)
    : ICommand<EventDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        Rules.Length(nameof(Title), Title, "Le titre", 5, 80)
            .Concat(Rules.Length(nameof(Description), Description, "La description", 20, 1000))
            .Concat(EventsCatalog.Categories.Contains(Category) ? [] : new[] { (nameof(Category), "Choisissez une catégorie.") })
            .Concat(Rules.Required(nameof(City), City, "La ville"))
            .Concat(Rules.Required(nameof(Address), Address, "L'adresse"));
}

public sealed record ToggleParticipationCommand(Guid EventId) : ICommand<ParticipationDto>;

public sealed class EventHandlers(IEventsDb db, ICommunitiesModule communities, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<GetEventsQuery, IReadOnlyList<EventDto>>,
    IRequestHandler<GetEventQuery, EventDto>,
    IRequestHandler<GetCommunityEventsQuery, IReadOnlyList<EventDto>>,
    IRequestHandler<GetParticipantEventsQuery, IReadOnlyList<EventDto>>,
    IRequestHandler<GetEventCategoriesQuery, IReadOnlyList<string>>,
    IRequestHandler<GetEventCitiesQuery, IReadOnlyList<string>>,
    IRequestHandler<CreateEventCommand, EventDto>,
    IRequestHandler<ToggleParticipationCommand, ParticipationDto>
{
    private IQueryable<CommunityEvent> Upcoming
    {
        get
        {
            var now = clock.GetUtcNow();
            return db.Events.AsNoTracking().Include(e => e.Participants).Where(e => e.EndsAt >= now);
        }
    }

    public async Task<IReadOnlyList<EventDto>> Handle(GetEventsQuery request, CancellationToken ct)
    {
        var query = Upcoming;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(q) || e.Description.ToLower().Contains(q) || e.City.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(e => e.Category == request.Category);
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            query = query.Where(e => e.City == request.City);
        }

        var today = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var endOfWeek = today.AddDays(7);
        var endOfMonth = today.AddDays(31);
        query = request.Period switch
        {
            EventPeriod.ThisWeek => query.Where(e => e.StartsAt < endOfWeek),
            EventPeriod.ThisMonth => query.Where(e => e.StartsAt < endOfMonth),
            EventPeriod.Later => query.Where(e => e.StartsAt >= endOfMonth),
            _ => query
        };

        return (await query.OrderBy(e => e.StartsAt).ToListAsync(ct)).Select(e => e.ToDto()).ToList();
    }

    public async Task<EventDto> Handle(GetEventQuery request, CancellationToken ct) =>
        (await db.Events.AsNoTracking().Include(e => e.Participants).FirstOrDefaultAsync(e => e.Id == request.Id, ct)
         ?? throw new NotFoundException("Événement")).ToDto();

    public async Task<IReadOnlyList<EventDto>> Handle(GetCommunityEventsQuery request, CancellationToken ct) =>
        (await Upcoming.Where(e => e.CommunityId == request.CommunityId).OrderBy(e => e.StartsAt).ToListAsync(ct))
        .Select(e => e.ToDto()).ToList();

    public async Task<IReadOnlyList<EventDto>> Handle(GetParticipantEventsQuery request, CancellationToken ct) =>
        (await Upcoming.Where(e => e.Participants.Any(p => p.UserId == request.UserId)).OrderBy(e => e.StartsAt).ToListAsync(ct))
        .Select(e => e.ToDto()).ToList();

    public Task<IReadOnlyList<string>> Handle(GetEventCategoriesQuery request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(EventsCatalog.Categories);

    public async Task<IReadOnlyList<string>> Handle(GetEventCitiesQuery request, CancellationToken ct) =>
        await db.Events.AsNoTracking().Select(e => e.City).Distinct().OrderBy(c => c).ToListAsync(ct);

    public async Task<EventDto> Handle(CreateEventCommand request, CancellationToken ct)
    {
        if (request.CommunityId is { } communityId
            && !await communities.IsMemberAsync(communityId, me.Id, ct))
        {
            throw new ForbiddenException("Rejoignez la communauté pour y organiser un événement.");
        }

        var created = CommunityEvent.Create(
            Guid.NewGuid(), request.Title, request.Description, request.Category, request.City, request.Country ?? "",
            request.Address, request.StartsAt.ToUniversalTime(), request.EndsAt.ToUniversalTime(), me.Id, request.CommunityId,
            request.ImagePath ?? "Events/event_01.jpg", request.Capacity, clock.GetUtcNow());

        db.Events.Add(created);
        await db.SaveChangesAsync(ct);
        return created.ToDto();
    }

    public async Task<ParticipationDto> Handle(ToggleParticipationCommand request, CancellationToken ct)
    {
        var ev = await db.Events.Include(e => e.Participants).FirstOrDefaultAsync(e => e.Id == request.EventId, ct)
                 ?? throw new NotFoundException("Événement");
        var participating = ev.ToggleParticipation(me.Id, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new ParticipationDto(participating, ev.ToDto());
    }
}
