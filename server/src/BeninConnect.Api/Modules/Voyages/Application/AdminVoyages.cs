using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Voyages.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Voyages.Application;

public enum AdminTripView
{
    All,
    Upcoming,
    Hidden,
    Reported
}

public sealed record VoyagesOverviewDto(
    int OpenTrips, int UpcomingTrips, int PendingBookings, int AcceptedBookings, int DeliveredBookings, int Disputes, int OpenReports, int HiddenTrips,
    int DeliveredLast30Days, IReadOnlyList<LabelCount> TopRoutes);

/// <summary>Console : le nom complet du membre est visible de l'équipe (jamais l'email ni le téléphone).</summary>
public sealed record AdminTripDto(
    Guid Id, Guid TravelerId, string TravelerName, string FromCountry, string FromCity, string ToCountry, string ToCity, DateOnly DepartureDate,
    decimal CapacityKg, decimal ReservedKg, TripStatus Status, TripHiddenReason HiddenReason, string? HiddenNote, int ReportCount, int BookingCount,
    DateTimeOffset CreatedAt);

public sealed record AdminBookingDto(
    Guid Id, Guid TripId, string Route, DateOnly DepartureDate, Guid SenderId, string SenderName, Guid TravelerId, string TravelerName, decimal WeightKg,
    ParcelCategory Category, string Description, BookingStatus Status, BookingStatus? StatusBeforeDispute, string? DisputeReason, Guid? DisputedBy,
    DateTimeOffset? DisputedAt, string? ResolutionNote, int DeliveryAttempts, DateTimeOffset CreatedAt, DateTimeOffset? DeliveredAt);

public sealed record AdminVoyageReportDto(
    Guid Id, VoyageReportTarget TargetType, Guid TargetId, string TargetLabel, Guid? TripId, Guid AuthorId, string AuthorName, string Reason,
    DateTimeOffset CreatedAt, bool Handled, string? Decision);

public sealed record AdminVoyageReviewDto(
    Guid Id, Guid BookingId, string AuthorName, string TargetName, bool AboutTraveler, int Rating, string? Comment, bool IsHidden, string? HiddenReason,
    int ReportCount, DateTimeOffset CreatedAt);

public sealed record AdminTripDetailDto(
    AdminTripDto Trip, IReadOnlyList<ParcelCategory> AcceptedCategories, decimal? PricePerKg, string Currency, string? DropOffZones, string? Notes,
    IReadOnlyList<AdminBookingDto> Bookings, IReadOnlyList<AdminVoyageReportDto> Reports, IReadOnlyList<AdminVoyageReviewDto> Reviews);

public sealed record GetVoyagesOverviewQuery : IQuery<VoyagesOverviewDto>;

public sealed record GetAdminTripsQuery(AdminTripView View, string? Q, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminTripDto>>;

public sealed record GetAdminTripQuery(Guid Id) : IQuery<AdminTripDetailDto>;

public sealed record HideTripCommand(Guid Id, string Reason) : ICommand<AdminTripDetailDto>;

public sealed record ShowTripCommand(Guid Id) : ICommand<AdminTripDetailDto>;

public sealed record GetDisputesQuery(bool Resolved, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminBookingDto>>;

public sealed record ResolveDisputeCommand(Guid BookingId, DisputeOutcome Outcome, string Note) : ICommand<AdminBookingDto>;

public sealed record GetAdminVoyageReportsQuery(bool Handled, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminVoyageReportDto>>;

public sealed record CloseVoyageReportCommand(Guid Id, string? Decision) : ICommand<AdminVoyageReportDto>;

public sealed record SetVoyageReviewVisibilityCommand(Guid Id, bool Visible, string? Reason) : ICommand<AdminVoyageReviewDto>;

/// <summary>Modération des voyages (permission ContentModerate) : trajets, litiges, signalements, avis. Chaque décision est journalisée.</summary>
public sealed class AdminVoyageHandlers(IVoyagesDb db, IIdentityModule identity, IEventBus bus, IAdminSession admin, TimeProvider clock) :
    IRequestHandler<GetVoyagesOverviewQuery, VoyagesOverviewDto>,
    IRequestHandler<GetAdminTripsQuery, PagedResult<AdminTripDto>>,
    IRequestHandler<GetAdminTripQuery, AdminTripDetailDto>,
    IRequestHandler<HideTripCommand, AdminTripDetailDto>,
    IRequestHandler<ShowTripCommand, AdminTripDetailDto>,
    IRequestHandler<GetDisputesQuery, PagedResult<AdminBookingDto>>,
    IRequestHandler<ResolveDisputeCommand, AdminBookingDto>,
    IRequestHandler<GetAdminVoyageReportsQuery, PagedResult<AdminVoyageReportDto>>,
    IRequestHandler<CloseVoyageReportCommand, AdminVoyageReportDto>,
    IRequestHandler<SetVoyageReviewVisibilityCommand, AdminVoyageReviewDto>
{
    public async Task<VoyagesOverviewDto> Handle(GetVoyagesOverviewQuery q, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = Trip.Today(now);
        var routes = await db.Trips.AsNoTracking().Where(t => t.CreatedAt > now.AddDays(-90))
            .GroupBy(t => new { t.FromCity, t.ToCity }).Select(g => new { g.Key.FromCity, g.Key.ToCity, Count = g.Count() }).ToListAsync(ct);
        return new VoyagesOverviewDto(
            await db.Trips.CountAsync(t => t.Status == TripStatus.Open, ct),
            await db.Trips.CountAsync(t => (t.Status == TripStatus.Open || t.Status == TripStatus.Full) && t.DepartureDate >= today, ct),
            await db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending, ct),
            await db.Bookings.CountAsync(b => b.Status == BookingStatus.Accepted, ct),
            await db.Bookings.CountAsync(b => b.Status == BookingStatus.Delivered, ct),
            await db.Bookings.CountAsync(b => b.Status == BookingStatus.Disputed, ct),
            await db.Reports.CountAsync(r => !r.Handled, ct),
            await db.Trips.CountAsync(t => t.HiddenReason != TripHiddenReason.None, ct),
            await db.Bookings.CountAsync(b => b.Status == BookingStatus.Delivered && b.DeliveredAt > now.AddDays(-30), ct),
            routes.OrderByDescending(r => r.Count).Take(8).Select(r => new LabelCount($"{r.FromCity} → {r.ToCity}", r.Count)).ToList());
    }

    public async Task<PagedResult<AdminTripDto>> Handle(GetAdminTripsQuery q, CancellationToken ct)
    {
        var (skip, take) = Text.Page(q.Skip, q.Take);
        var today = Trip.Today(clock.GetUtcNow());
        var query = db.Trips.AsNoTracking();
        query = q.View switch
        {
            AdminTripView.Upcoming => query.Where(t => (t.Status == TripStatus.Open || t.Status == TripStatus.Full) && t.DepartureDate >= today),
            AdminTripView.Hidden => query.Where(t => t.HiddenReason != TripHiddenReason.None),
            AdminTripView.Reported => query.Where(t => t.ReportCount > 0),
            _ => query
        };
        if (Text.Like(q.Q) is { } search)
        {
            var key = VoyageRules.CityKey(search);
            query = query.Where(t => t.FromCityKey.Contains(key) || t.ToCityKey.Contains(key));
        }

        var total = await query.CountAsync(ct);
        var page = await query.Include(t => t.Bookings).OrderByDescending(t => t.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminTripDto>(await TripsAsync(page, ct), total, skip);
    }

    public async Task<AdminTripDetailDto> Handle(GetAdminTripQuery q, CancellationToken ct) => await DetailAsync(q.Id, ct);

    public async Task<AdminTripDetailDto> Handle(HideTripCommand c, CancellationToken ct)
    {
        var reason = VoyageRules.Reason(c.Reason);
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == c.Id, ct) ?? throw new NotFoundException("Trajet");
        trip.Hide(TripHiddenReason.Admin, reason, clock.GetUtcNow());
        await CloseReportsAsync(VoyageReportTarget.Trip, trip.Id, $"Trajet masqué : {reason}", ct);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Trajet masqué", "Trip", trip.Id, reason, ct);
        await VoyagesData.NotifyAsync(bus, trip.TravelerId, "Votre trajet est masqué", reason, null, ct);
        return await DetailAsync(trip.Id, ct);
    }

    public async Task<AdminTripDetailDto> Handle(ShowTripCommand c, CancellationToken ct)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == c.Id, ct) ?? throw new NotFoundException("Trajet");
        trip.Show(clock.GetUtcNow());
        await CloseReportsAsync(VoyageReportTarget.Trip, trip.Id, "Trajet rétabli", ct);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Trajet rétabli", "Trip", trip.Id, VoyagesData.Route(trip), ct);
        return await DetailAsync(trip.Id, ct);
    }

    public async Task<PagedResult<AdminBookingDto>> Handle(GetDisputesQuery q, CancellationToken ct)
    {
        var (skip, take) = Text.Page(q.Skip, q.Take);
        var query = q.Resolved
            ? db.Bookings.AsNoTracking().Where(b => b.ResolvedAt != null)
            : db.Bookings.AsNoTracking().Where(b => b.Status == BookingStatus.Disputed);
        var total = await query.CountAsync(ct);
        var page = await (q.Resolved ? query.OrderByDescending(b => b.ResolvedAt) : query.OrderBy(b => b.DisputedAt)).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminBookingDto>(await BookingsAsync(page, ct), total, skip);
    }

    public async Task<AdminBookingDto> Handle(ResolveDisputeCommand c, CancellationToken ct)
    {
        var tripId = await db.Bookings.Where(b => b.Id == c.BookingId).Select(b => (Guid?)b.TripId).FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Réservation");
        var trip = await db.Trips.Include(t => t.Bookings).FirstAsync(t => t.Id == tripId, ct);
        var booking = trip.Find(c.BookingId);
        booking.Resolve(c.Outcome, c.Note, clock.GetUtcNow());
        await CloseReportsAsync(VoyageReportTarget.Booking, booking.Id, $"Litige tranché : {booking.ResolutionNote}", ct);
        await db.SaveChangesAsync(ct);
        var label = c.Outcome == DisputeOutcome.Delivered ? "livraison confirmée" : "réservation annulée";
        await admin.LogAsync($"Litige tranché ({label})", "Booking", booking.Id, booking.ResolutionNote ?? "", ct);
        foreach (var user in new[] { booking.SenderId, booking.TravelerId })
        {
            await VoyagesData.NotifyAsync(bus, user, "Litige tranché", $"Décision : {label}. {booking.ResolutionNote}", booking.ConversationId, ct);
        }

        return (await BookingsAsync([booking], ct))[0];
    }

    public async Task<PagedResult<AdminVoyageReportDto>> Handle(GetAdminVoyageReportsQuery q, CancellationToken ct)
    {
        var (skip, take) = Text.Page(q.Skip, q.Take);
        var query = db.Reports.AsNoTracking().Where(r => r.Handled == q.Handled);
        var total = await query.CountAsync(ct);
        var page = await (q.Handled ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt)).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminVoyageReportDto>(await ReportsAsync(page, ct), total, skip);
    }

    public async Task<AdminVoyageReportDto> Handle(CloseVoyageReportCommand c, CancellationToken ct)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == c.Id, ct) ?? throw new NotFoundException("Signalement");
        report.Close(string.IsNullOrWhiteSpace(c.Decision) ? "Classé sans suite" : c.Decision.Trim());
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Signalement de voyage traité", "TripReport", report.Id, report.Decision ?? "", ct);
        return (await ReportsAsync([report], ct))[0];
    }

    public async Task<AdminVoyageReviewDto> Handle(SetVoyageReviewVisibilityCommand c, CancellationToken ct)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == c.Id, ct) ?? throw new NotFoundException("Avis");
        if (c.Visible)
        {
            review.Show();
        }
        else
        {
            review.Hide(c.Reason ?? "");
        }

        await CloseReportsAsync(VoyageReportTarget.Review, review.Id, c.Visible ? "Avis rétabli" : $"Avis masqué : {review.HiddenReason}", ct);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync(c.Visible ? "Avis de voyage rétabli" : "Avis de voyage masqué", "TripReview", review.Id, review.HiddenReason ?? "", ct);
        return (await ReviewsAsync([review], ct))[0];
    }

    private async Task CloseReportsAsync(VoyageReportTarget target, Guid id, string decision, CancellationToken ct)
    {
        foreach (var report in await db.Reports.Where(r => r.TargetType == target && r.TargetId == id && !r.Handled).ToListAsync(ct))
        {
            report.Close(decision);
        }
    }

    private async Task<AdminTripDetailDto> DetailAsync(Guid id, CancellationToken ct)
    {
        var trip = await db.Trips.AsNoTracking().Include(t => t.Bookings).FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Trajet");
        var bookingIds = trip.Bookings.Select(b => b.Id).ToList();
        var reviews = await db.Reviews.AsNoTracking().Where(r => r.TripId == trip.Id).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var reviewIds = reviews.Select(r => r.Id).ToList();
        var reports = await db.Reports.AsNoTracking()
            .Where(r => (r.TargetType == VoyageReportTarget.Trip && r.TargetId == trip.Id)
                        || (r.TargetType == VoyageReportTarget.Booking && bookingIds.Contains(r.TargetId))
                        || (r.TargetType == VoyageReportTarget.Review && reviewIds.Contains(r.TargetId)))
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return new AdminTripDetailDto((await TripsAsync([trip], ct))[0], trip.AcceptedCategories, trip.PricePerKg, trip.Currency, trip.DropOffZones, trip.Notes,
            await BookingsAsync(trip.Bookings.OrderByDescending(b => b.CreatedAt).ToList(), ct), await ReportsAsync(reports, ct), await ReviewsAsync(reviews, ct));
    }

    private async Task<List<AdminTripDto>> TripsAsync(IReadOnlyList<Trip> trips, CancellationToken ct)
    {
        var names = await identity.GetNamesAsync(trips.Select(t => t.TravelerId), ct);
        return trips.Select(t => new AdminTripDto(t.Id, t.TravelerId, names.GetValueOrDefault(t.TravelerId) ?? "Membre supprimé", t.FromCountry, t.FromCity,
            t.ToCountry, t.ToCity, t.DepartureDate, t.CapacityKg, t.ReservedKg, t.Status, t.HiddenReason, t.HiddenNote, t.ReportCount, t.Bookings.Count,
            t.CreatedAt)).ToList();
    }

    private async Task<List<AdminBookingDto>> BookingsAsync(IReadOnlyList<Booking> bookings, CancellationToken ct)
    {
        var tripIds = bookings.Select(b => b.TripId).Distinct().ToList();
        var trips = await db.Trips.AsNoTracking().Where(t => tripIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var names = await identity.GetNamesAsync(bookings.SelectMany(b => new[] { b.SenderId, b.TravelerId }), ct);
        return bookings.Select(b =>
        {
            var trip = trips.GetValueOrDefault(b.TripId);
            return new AdminBookingDto(b.Id, b.TripId, trip is null ? "Trajet supprimé" : VoyagesData.Route(trip), trip?.DepartureDate ?? default, b.SenderId,
                names.GetValueOrDefault(b.SenderId) ?? "Membre supprimé", b.TravelerId, names.GetValueOrDefault(b.TravelerId) ?? "Membre supprimé", b.WeightKg,
                b.Category, b.Description, b.Status, b.StatusBeforeDispute, b.DisputeReason, b.DisputedBy, b.DisputedAt, b.ResolutionNote, b.DeliveryAttempts,
                b.CreatedAt, b.DeliveredAt);
        }).ToList();
    }

    private async Task<List<AdminVoyageReportDto>> ReportsAsync(IReadOnlyList<TripReport> reports, CancellationToken ct)
    {
        var names = await identity.GetNamesAsync(reports.Select(r => r.AuthorId), ct);
        var tripIds = reports.Where(r => r.TargetType == VoyageReportTarget.Trip).Select(r => r.TargetId).ToList();
        var bookingIds = reports.Where(r => r.TargetType == VoyageReportTarget.Booking).Select(r => r.TargetId).ToList();
        var reviewIds = reports.Where(r => r.TargetType == VoyageReportTarget.Review).Select(r => r.TargetId).ToList();
        var bookingTrips = await db.Bookings.AsNoTracking().Where(b => bookingIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.TripId, ct);
        var reviews = await db.Reviews.AsNoTracking().Where(r => reviewIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var allTripIds = tripIds.Concat(bookingTrips.Values).Concat(reviews.Values.Select(r => r.TripId)).Distinct().ToList();
        var trips = await db.Trips.AsNoTracking().Where(t => allTripIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        return reports.Select(r =>
        {
            Guid? tripId = r.TargetType switch
            {
                VoyageReportTarget.Trip => r.TargetId,
                VoyageReportTarget.Booking => bookingTrips.TryGetValue(r.TargetId, out var t) ? t : null,
                _ => reviews.TryGetValue(r.TargetId, out var rv) ? rv.TripId : null
            };
            var route = tripId is { } id && trips.TryGetValue(id, out var trip) ? VoyagesData.Route(trip) : "supprimé";
            var label = r.TargetType switch
            {
                VoyageReportTarget.Trip => $"Trajet {route}",
                VoyageReportTarget.Booking => $"Réservation ({route})",
                _ => reviews.TryGetValue(r.TargetId, out var review) ? $"Avis {review.Rating}/5 ({route})" : "Avis supprimé"
            };
            return new AdminVoyageReportDto(r.Id, r.TargetType, r.TargetId, label, tripId, r.AuthorId, names.GetValueOrDefault(r.AuthorId) ?? "Membre supprimé",
                r.Reason, r.CreatedAt, r.Handled, r.Decision);
        }).ToList();
    }

    private async Task<List<AdminVoyageReviewDto>> ReviewsAsync(IReadOnlyList<TripReview> reviews, CancellationToken ct)
    {
        var names = await identity.GetNamesAsync(reviews.SelectMany(r => new[] { r.AuthorId, r.TargetId }), ct);
        return reviews.Select(r => new AdminVoyageReviewDto(r.Id, r.BookingId, names.GetValueOrDefault(r.AuthorId) ?? "Membre supprimé",
            names.GetValueOrDefault(r.TargetId) ?? "Membre supprimé", r.AboutTraveler, r.Rating, r.Comment, r.IsHidden, r.HiddenReason, r.ReportCount,
            r.CreatedAt)).ToList();
    }
}
