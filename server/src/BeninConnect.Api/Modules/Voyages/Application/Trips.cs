using System.Text;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Voyages.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Voyages.Application;

public sealed record SearchTripsQuery(
    string? From, string? To, string? FromCity, string? ToCity, DateOnly? After, DateOnly? Before, ParcelCategory? Category, decimal? MinKg,
    string? Cursor, int Take = 20) : IQuery<TripPageDto>;

public sealed record GetTripQuery(Guid Id) : IQuery<TripDetailDto>;

public sealed record PublishTripCommand(TripDetails Details) : ICommand<TripDetailDto>;

public sealed record UpdateTripCommand(Guid Id, TripDetails Details) : ICommand<TripDetailDto>;

public sealed record CancelTripCommand(Guid Id) : ICommand<TripDetailDto>;

public sealed record GetMyTripsQuery : IQuery<IReadOnlyList<MyTripDto>>;

public sealed record GetVoyagesInfoQuery : IQuery<VoyagesInfoDto>;

public sealed record GetVoyageStatsQuery(Guid UserId) : IQuery<VoyageStatsDto>;

public sealed record GetVoyageReviewsQuery(Guid UserId) : IQuery<VoyageReviewPageDto>;

/// <summary>Trajets : recherche, publication par le voyageur, fiche et statistiques du profil.</summary>
public sealed class TripHandlers(IVoyagesDb db, IIdentityModule identity, IEventBus bus, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<SearchTripsQuery, TripPageDto>,
    IRequestHandler<GetTripQuery, TripDetailDto>,
    IRequestHandler<PublishTripCommand, TripDetailDto>,
    IRequestHandler<UpdateTripCommand, TripDetailDto>,
    IRequestHandler<CancelTripCommand, TripDetailDto>,
    IRequestHandler<GetMyTripsQuery, IReadOnlyList<MyTripDto>>,
    IRequestHandler<GetVoyagesInfoQuery, VoyagesInfoDto>,
    IRequestHandler<GetVoyageStatsQuery, VoyageStatsDto>,
    IRequestHandler<GetVoyageReviewsQuery, VoyageReviewPageDto>
{
    public async Task<TripPageDto> Handle(SearchTripsQuery q, CancellationToken ct)
    {
        var today = Trip.Today(clock.GetUtcNow());
        var blocked = await identity.GetBlockedRelationsAsync(me.Id, ct);
        var query = db.Trips.AsNoTracking().Where(t => t.Status == TripStatus.Open && t.HiddenReason == TripHiddenReason.None && t.DepartureDate >= today);
        if (!string.IsNullOrWhiteSpace(q.From))
        {
            var from = VoyageRules.Country(q.From, "Départ");
            query = query.Where(t => t.FromCountry == from);
        }

        if (!string.IsNullOrWhiteSpace(q.To))
        {
            var to = VoyageRules.Country(q.To, "Arrivée");
            query = query.Where(t => t.ToCountry == to);
        }

        if (!string.IsNullOrWhiteSpace(q.FromCity))
        {
            var key = VoyageRules.CityKey(q.FromCity);
            query = query.Where(t => t.FromCityKey == key);
        }

        if (!string.IsNullOrWhiteSpace(q.ToCity))
        {
            var key = VoyageRules.CityKey(q.ToCity);
            query = query.Where(t => t.ToCityKey == key);
        }

        if (q.After is { } after)
        {
            query = query.Where(t => t.DepartureDate >= after);
        }

        if (q.Before is { } before)
        {
            query = query.Where(t => t.DepartureDate <= before);
        }

        if (blocked.Count > 0)
        {
            var ids = blocked.ToList();
            query = query.Where(t => !ids.Contains(t.TravelerId));
        }

        // Catégorie et kilos restants : filtrés en mémoire (collection et somme des réservations)
        var candidates = await query.Include(t => t.Bookings).OrderBy(t => t.DepartureDate).ThenBy(t => t.CreatedAt).Take(500).ToListAsync(ct);
        var filtered = candidates.Where(t => (q.Category is null || t.AcceptedCategories.Contains(q.Category.Value)) && t.RemainingKg >= (q.MinKg ?? 0.1m))
            .ToList();
        var offset = Cursor.Decode(q.Cursor);
        var take = Math.Clamp(q.Take, 1, 50);
        var page = filtered.Skip(offset).Take(take).ToList();
        var next = offset + page.Count < filtered.Count ? Cursor.Encode(offset + page.Count) : null;
        return new TripPageDto(await CardsAsync(page, ct), next, filtered.Count);
    }

    public async Task<TripDetailDto> Handle(GetTripQuery q, CancellationToken ct)
    {
        var trip = await VisibleTripAsync(q.Id, ct);
        return await DetailAsync(trip, ct);
    }

    public async Task<TripDetailDto> Handle(PublishTripCommand c, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (await db.Trips.CountAsync(t => t.TravelerId == me.Id && t.CreatedAt > now.AddDays(-1), ct) >= VoyageRules.MaxTripsPerDay)
        {
            throw new DomainException($"Limite atteinte : {VoyageRules.MaxTripsPerDay} trajets par jour.");
        }

        var trip = Trip.Create(me.Id, c.Details, now);
        db.Trips.Add(trip);
        await db.SaveChangesAsync(ct);
        await NotifyAlertsAsync(trip, ct);
        return await DetailAsync(trip, ct);
    }

    public async Task<TripDetailDto> Handle(UpdateTripCommand c, CancellationToken ct)
    {
        var trip = await OwnTripAsync(c.Id, ct);
        trip.Update(me.Id, c.Details, clock.GetUtcNow());
        await SaveAsync(ct);
        return await DetailAsync(trip, ct);
    }

    public async Task<TripDetailDto> Handle(CancelTripCommand c, CancellationToken ct)
    {
        var trip = await OwnTripAsync(c.Id, ct);
        var affected = trip.Cancel(me.Id, clock.GetUtcNow());
        await SaveAsync(ct);
        foreach (var booking in affected)
        {
            await VoyagesData.NotifyAsync(bus, booking.SenderId, "Trajet annulé",
                $"Le voyage {VoyagesData.Route(trip)} du {VoyagesData.Day(trip.DepartureDate)} est annulé : votre réservation aussi.", booking.ConversationId, ct);
        }

        return await DetailAsync(trip, ct);
    }

    public async Task<IReadOnlyList<MyTripDto>> Handle(GetMyTripsQuery q, CancellationToken ct)
    {
        var trips = await db.Trips.AsNoTracking().Include(t => t.Bookings).Where(t => t.TravelerId == me.Id)
            .OrderByDescending(t => t.DepartureDate).Take(100).ToListAsync(ct);
        var bookings = trips.SelectMany(t => t.Bookings).ToList();
        var described = await BookingViews.DescribeAsync(db, identity, me.Id, bookings, trips, ct);
        var result = new List<MyTripDto>();
        foreach (var trip in trips)
        {
            var list = described.Where(b => b.TripId == trip.Id).OrderBy(b => b.Status == BookingStatus.Pending ? 0 : 1).ThenByDescending(b => b.CreatedAt).ToList();
            result.Add(new MyTripDto(await DetailAsync(trip, ct), list, list.Count(b => b.Status == BookingStatus.Pending)));
        }

        return result;
    }

    public Task<VoyagesInfoDto> Handle(GetVoyagesInfoQuery q, CancellationToken ct) =>
        Task.FromResult(new VoyagesInfoDto(VoyagesLegal.Intermediation, VoyagesLegal.TravelerResponsibility, VoyagesLegal.ProhibitedItems,
            VoyagesLegal.TravelerAdvice, VoyagesLegal.CustomsLinks.Select(l => new LinkDto(l.Label, l.Url)).ToList(), VoyageRules.MaxDaysAhead,
            VoyageRules.MinBookingKg, VoyageRules.MaxBookingKg, VoyageRules.MaxCapacityKg, VoyageRules.MaxAlertsPerMember));

    public async Task<VoyageStatsDto> Handle(GetVoyageStatsQuery q, CancellationToken ct)
    {
        if ((await identity.GetBlockedRelationsAsync(me.Id, ct)).Contains(q.UserId))
        {
            throw new NotFoundException("Membre");
        }

        var score = (await VoyagesData.ScoresAsync(db, [q.UserId], ct))[q.UserId];
        var shipments = await db.Bookings.CountAsync(b => b.SenderId == q.UserId && b.Status == BookingStatus.Delivered, ct);
        var today = Trip.Today(clock.GetUtcNow());
        var upcoming = await db.Trips.AsNoTracking().Include(t => t.Bookings)
            .Where(t => t.TravelerId == q.UserId && t.Status == TripStatus.Open && t.HiddenReason == TripHiddenReason.None && t.DepartureDate >= today)
            .OrderBy(t => t.DepartureDate).Take(5).ToListAsync(ct);
        return new VoyageStatsDto(score.Deliveries, shipments, score.RatingAverage, score.RatingCount, score.Trusted, await CardsAsync(upcoming, ct));
    }

    public async Task<VoyageReviewPageDto> Handle(GetVoyageReviewsQuery q, CancellationToken ct)
    {
        var blocked = await identity.GetBlockedRelationsAsync(me.Id, ct);
        if (blocked.Contains(q.UserId))
        {
            throw new NotFoundException("Membre");
        }

        var reviews = (await db.Reviews.AsNoTracking().Where(r => r.TargetId == q.UserId && !r.IsHidden)
            .OrderByDescending(r => r.CreatedAt).Take(50).ToListAsync(ct)).Where(r => !blocked.Contains(r.AuthorId)).ToList();
        var people = await VoyagesData.PeopleAsync(identity, reviews.Select(r => r.AuthorId), ct);
        var aboutTraveler = reviews.Where(r => r.AboutTraveler).ToList();
        return new VoyageReviewPageDto(
            reviews.Select(r => new VoyageReviewDto(r.Id, VoyagesData.Person(people, r.AuthorId), r.Rating, r.Comment, r.CreatedAt, r.AboutTraveler)).ToList(),
            aboutTraveler.Count == 0 ? 0 : Math.Round(aboutTraveler.Average(r => r.Rating), 1), aboutTraveler.Count);
    }

    /// <summary>Alertes correspondantes : une notification par membre (hors voyageur et membres bloqués).</summary>
    private async Task NotifyAlertsAsync(Trip trip, CancellationToken ct)
    {
        var alerts = await db.Alerts.Where(a => a.FromCountry == trip.FromCountry && a.ToCountry == trip.ToCountry && a.UserId != trip.TravelerId)
            .ToListAsync(ct);
        var matching = alerts.Where(a => a.Matches(trip)).ToList();
        if (matching.Count == 0)
        {
            return;
        }

        var blocked = await identity.GetBlockedRelationsAsync(trip.TravelerId, ct);
        var now = clock.GetUtcNow();
        var notified = new HashSet<Guid>();
        foreach (var alert in matching.Where(a => !blocked.Contains(a.UserId)))
        {
            alert.MarkNotified(now);
            if (notified.Add(alert.UserId))
            {
                await VoyagesData.NotifyProfileAsync(bus, alert.UserId, "Un membre part bientôt",
                    $"{VoyagesData.Route(trip)} le {VoyagesData.Day(trip.DepartureDate)} : {trip.CapacityKg:0.#} kg disponibles.", trip.TravelerId, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<Trip> VisibleTripAsync(Guid id, CancellationToken ct)
    {
        var trip = await db.Trips.AsNoTracking().Include(t => t.Bookings).FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Trajet");
        if (trip.TravelerId == me.Id)
        {
            return trip;
        }

        if (!trip.IsVisible || (await identity.GetBlockedRelationsAsync(me.Id, ct)).Contains(trip.TravelerId))
        {
            throw new NotFoundException("Trajet");
        }

        return trip;
    }

    private async Task<Trip> OwnTripAsync(Guid id, CancellationToken ct) =>
        await db.Trips.Include(t => t.Bookings).FirstOrDefaultAsync(t => t.Id == id && t.TravelerId == me.Id, ct) ?? throw new NotFoundException("Trajet");

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Le trajet vient d'être modifié : rechargez et réessayez.");
        }
    }

    private async Task<List<TripCardDto>> CardsAsync(IReadOnlyList<Trip> trips, CancellationToken ct)
    {
        var people = await VoyagesData.PeopleAsync(identity, trips.Select(t => t.TravelerId), ct);
        var scores = await VoyagesData.ScoresAsync(db, trips.Select(t => t.TravelerId), ct);
        return trips.Select(t => Card(t, VoyagesData.Person(people, t.TravelerId), scores[t.TravelerId])).ToList();
    }

    private TripCardDto Card(Trip t, PersonDto traveler, TravelerScore score) =>
        new(t.Id, traveler, t.FromCountry, t.FromCity, t.ToCountry, t.ToCity, t.DepartureDate, t.CapacityKg, t.RemainingKg, t.PricePerKg, t.Currency,
            t.AcceptedCategories, t.Status, score.RatingAverage, score.RatingCount, score.Trusted, t.TravelerId == me.Id);

    private async Task<TripDetailDto> DetailAsync(Trip trip, CancellationToken ct)
    {
        var card = (await CardsAsync([trip], ct))[0];
        var score = (await VoyagesData.ScoresAsync(db, [trip.TravelerId], ct))[trip.TravelerId];
        var mine = trip.Bookings.Where(b => b.SenderId == me.Id).OrderByDescending(b => b.CreatedAt).FirstOrDefault();
        var myBooking = mine is null ? null : (await BookingViews.DescribeAsync(db, identity, me.Id, [mine], [trip], ct))[0];
        var canBook = trip.TravelerId != me.Id && trip.IsBookable(Trip.Today(clock.GetUtcNow())) && trip.RemainingKg >= VoyageRules.MinBookingKg
                      && mine is not { IsActive: true };
        return new TripDetailDto(card, trip.DropOffZones, trip.Notes, score.Deliveries, myBooking, canBook,
            trip.TravelerId == me.Id ? trip.HiddenReason : TripHiddenReason.None, trip.CreatedAt);
    }
}

/// <summary>Curseur opaque de pagination (position dans la liste triée).</summary>
public static class Cursor
{
    public static string Encode(int offset) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"o:{offset}")).TrimEnd('=');

    public static int Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.PadRight(cursor.Length + (4 - cursor.Length % 4) % 4, '=')));
            return text.StartsWith("o:", StringComparison.Ordinal) && int.TryParse(text[2..], out var offset) && offset >= 0 ? offset : throw new FormatException();
        }
        catch (FormatException)
        {
            throw new DomainException("Curseur invalide.");
        }
    }
}
