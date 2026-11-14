using System.Security.Cryptography;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Voyages.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Voyages.Application;

public sealed record RequestBookingCommand(Guid TripId, decimal WeightKg, ParcelCategory Category, string Description, bool Certified) : ICommand<BookingDto>;

public sealed record AcceptBookingCommand(Guid Id) : ICommand<BookingDto>;

public sealed record DeclineBookingCommand(Guid Id, string? Reason) : ICommand<BookingDto>;

public sealed record CancelBookingCommand(Guid Id) : ICommand<BookingDto>;

public sealed record IssueDeliveryCodeCommand(Guid Id) : ICommand<DeliveryCodeDto>;

public sealed record DeliverBookingCommand(Guid Id, string Code) : ICommand<BookingDto>;

public sealed record DisputeBookingCommand(Guid Id, string Reason) : ICommand<BookingDto>;

public sealed record ReviewBookingCommand(Guid Id, int Rating, string? Comment) : ICommand<VoyageReviewDto>;

public sealed record GetMyBookingsQuery : IQuery<IReadOnlyList<BookingDto>>;

public sealed record GetBookingQuery(Guid Id) : IQuery<BookingDto>;

public sealed record ReportVoyageCommand(VoyageReportTarget TargetType, Guid TargetId, string Reason) : ICommand<CountDto>;

/// <summary>Réservations : demande → acceptation (conversation ouverte) → code de remise → livraison → avis.</summary>
public sealed class BookingHandlers(
    IVoyagesDb db, IIdentityModule identity, IMessagingModule messaging, IPasswordHasher hasher, IEventBus bus, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<RequestBookingCommand, BookingDto>,
    IRequestHandler<AcceptBookingCommand, BookingDto>,
    IRequestHandler<DeclineBookingCommand, BookingDto>,
    IRequestHandler<CancelBookingCommand, BookingDto>,
    IRequestHandler<IssueDeliveryCodeCommand, DeliveryCodeDto>,
    IRequestHandler<DeliverBookingCommand, BookingDto>,
    IRequestHandler<DisputeBookingCommand, BookingDto>,
    IRequestHandler<ReviewBookingCommand, VoyageReviewDto>,
    IRequestHandler<GetMyBookingsQuery, IReadOnlyList<BookingDto>>,
    IRequestHandler<GetBookingQuery, BookingDto>,
    IRequestHandler<ReportVoyageCommand, CountDto>
{
    public async Task<BookingDto> Handle(RequestBookingCommand c, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var trip = await db.Trips.Include(t => t.Bookings).FirstOrDefaultAsync(t => t.Id == c.TripId, ct) ?? throw new NotFoundException("Trajet");
        if (trip.TravelerId != me.Id && (!trip.IsVisible || (await identity.GetBlockedRelationsAsync(me.Id, ct)).Contains(trip.TravelerId)))
        {
            throw new NotFoundException("Trajet");
        }

        if (await db.Bookings.CountAsync(b => b.SenderId == me.Id && b.CreatedAt > now.AddDays(-1), ct) >= VoyageRules.MaxBookingRequestsPerDay)
        {
            throw new DomainException($"Limite atteinte : {VoyageRules.MaxBookingRequestsPerDay} demandes de réservation par jour.");
        }

        var booking = trip.RequestBooking(me.Id, c.WeightKg, c.Category, c.Description, c.Certified, now);
        await SaveAsync(ct);
        await VoyagesData.NotifyAsync(bus, trip.TravelerId, "Nouvelle demande de réservation",
            $"{await NameAsync(me.Id, ct)} demande {booking.WeightKg:0.#} kg sur votre voyage {VoyagesData.Route(trip)} du {VoyagesData.Day(trip.DepartureDate)}.",
            null, ct);
        return await DescribeAsync(booking, trip, ct);
    }

    public async Task<BookingDto> Handle(AcceptBookingCommand c, CancellationToken ct)
    {
        var (trip, booking) = await LoadAsync(c.Id, ct);
        if ((await identity.GetBlockedRelationsAsync(me.Id, ct)).Contains(booking.SenderId))
        {
            throw new DomainException("Vous ne pouvez plus échanger avec ce membre.");
        }

        trip.Accept(booking.Id, me.Id, clock.GetUtcNow());
        await SaveAsync(ct);

        // La conversation directe s'ouvre avec un message du voyageur (refusé si l'un a bloqué l'autre)
        var text = $"Réservation de {booking.WeightKg:0.#} kg acceptée pour le voyage {VoyagesData.Route(trip)} du {trip.DepartureDate:dd/MM/yyyy}. " +
                   "Convenons ici du lieu et de l'heure de remise. Le colis sera ouvert et vérifié à la remise.";
        booking.AttachConversation(await messaging.SendDirectAsync(trip.TravelerId, booking.SenderId, text, notifyRecipient: false, ct));
        await db.SaveChangesAsync(ct);
        await VoyagesData.NotifyAsync(bus, booking.SenderId, "Réservation acceptée",
            $"{booking.WeightKg:0.#} kg réservés sur {VoyagesData.Route(trip)} le {VoyagesData.Day(trip.DepartureDate)}. Générez le code de remise pour votre destinataire.",
            booking.ConversationId, ct);
        return await DescribeAsync(booking, trip, ct);
    }

    public async Task<BookingDto> Handle(DeclineBookingCommand c, CancellationToken ct)
    {
        var (trip, booking) = await LoadAsync(c.Id, ct);
        trip.Decline(booking.Id, me.Id, c.Reason, clock.GetUtcNow());
        await SaveAsync(ct);
        await VoyagesData.NotifyAsync(bus, booking.SenderId, "Réservation refusée",
            $"Le voyageur ne peut pas prendre votre colis ({VoyagesData.Route(trip)})." + (booking.DeclineReason is null ? "" : $" Motif : {booking.DeclineReason}"),
            null, ct);
        return await DescribeAsync(booking, trip, ct);
    }

    public async Task<BookingDto> Handle(CancelBookingCommand c, CancellationToken ct)
    {
        var (trip, booking) = await LoadAsync(c.Id, ct);
        trip.CancelBooking(booking.Id, me.Id, clock.GetUtcNow());
        await SaveAsync(ct);
        var other = me.Id == booking.SenderId ? trip.TravelerId : booking.SenderId;
        await VoyagesData.NotifyAsync(bus, other, "Réservation annulée",
            $"{await NameAsync(me.Id, ct)} a annulé la réservation de {booking.WeightKg:0.#} kg ({VoyagesData.Route(trip)}).", booking.ConversationId, ct);
        return await DescribeAsync(booking, trip, ct);
    }

    public async Task<DeliveryCodeDto> Handle(IssueDeliveryCodeCommand c, CancellationToken ct)
    {
        var (trip, booking) = await LoadAsync(c.Id, ct);
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        booking.SetDeliveryCode(me.Id, hasher.Hash(code), clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        var share = $"Code de remise BéninConnect : {code}. Donnez-le au voyageur seulement quand vous avez reçu le colis ({VoyagesData.Route(trip)}).";
        return new DeliveryCodeDto(code, share);
    }

    public async Task<BookingDto> Handle(DeliverBookingCommand c, CancellationToken ct)
    {
        var (trip, booking) = await LoadAsync(c.Id, ct);
        var delivered = booking.TryDeliver(me.Id, c.Code, hasher.Verify, clock.GetUtcNow());
        await db.SaveChangesAsync(ct); // l'essai raté est compté avant de répondre
        if (!delivered)
        {
            if (booking.CodeLocked)
            {
                await VoyagesData.NotifyAsync(bus, booking.SenderId, "Code de remise bloqué",
                    "Trop d'essais erronés : générez un nouveau code et transmettez-le à votre destinataire.", booking.ConversationId, ct);
                throw new DomainException("Code erroné. Trop d'essais : l'expéditeur doit générer un nouveau code.");
            }

            throw new DomainException($"Code erroné. Il reste {VoyageRules.DeliveryCodeAttempts - booking.DeliveryAttempts} essai(s).");
        }

        await VoyagesData.NotifyAsync(bus, booking.SenderId, "Colis livré",
            $"Votre colis est arrivé ({VoyagesData.Route(trip)}). Laissez un avis sur le voyageur.", booking.ConversationId, ct);
        return await DescribeAsync(booking, trip, ct);
    }

    public async Task<BookingDto> Handle(DisputeBookingCommand c, CancellationToken ct)
    {
        var (trip, booking) = await LoadAsync(c.Id, ct);
        booking.Dispute(me.Id, c.Reason, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        var other = me.Id == booking.SenderId ? trip.TravelerId : booking.SenderId;
        await VoyagesData.NotifyAsync(bus, other, "Litige ouvert",
            "Un litige est ouvert sur votre réservation. L'équipe BéninConnect va examiner la situation.", booking.ConversationId, ct);
        return await DescribeAsync(booking, trip, ct);
    }

    public async Task<VoyageReviewDto> Handle(ReviewBookingCommand c, CancellationToken ct)
    {
        var (_, booking) = await LoadAsync(c.Id, ct);
        if (await db.Reviews.AnyAsync(r => r.BookingId == booking.Id && r.AuthorId == me.Id, ct))
        {
            throw new ConflictException("Vous avez déjà laissé un avis pour cette livraison.");
        }

        var review = TripReview.Create(booking, me.Id, c.Rating, c.Comment, clock.GetUtcNow());
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);
        await VoyagesData.NotifyAsync(bus, review.TargetId, $"Nouvel avis : {review.Rating}/5", review.Comment ?? "Un membre a noté votre échange.", null, ct);
        var people = await VoyagesData.PeopleAsync(identity, [me.Id], ct);
        return new VoyageReviewDto(review.Id, VoyagesData.Person(people, me.Id), review.Rating, review.Comment, review.CreatedAt, review.AboutTraveler);
    }

    public async Task<IReadOnlyList<BookingDto>> Handle(GetMyBookingsQuery q, CancellationToken ct)
    {
        var bookings = await db.Bookings.AsNoTracking().Where(b => b.SenderId == me.Id).OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync(ct);
        var tripIds = bookings.Select(b => b.TripId).Distinct().ToList();
        var trips = await db.Trips.AsNoTracking().Where(t => tripIds.Contains(t.Id)).ToListAsync(ct);
        return await BookingViews.DescribeAsync(db, identity, me.Id, bookings, trips, ct);
    }

    public async Task<BookingDto> Handle(GetBookingQuery q, CancellationToken ct)
    {
        var (trip, booking) = await LoadAsync(q.Id, ct);
        return await DescribeAsync(booking, trip, ct);
    }

    /// <summary>Un signalement par membre et par cible ; un trajet ou un avis est masqué au seuil, en attendant la revue.</summary>
    public async Task<CountDto> Handle(ReportVoyageCommand c, CancellationToken ct)
    {
        if (await db.Reports.AnyAsync(r => r.TargetType == c.TargetType && r.TargetId == c.TargetId && r.AuthorId == me.Id, ct))
        {
            throw new ConflictException("Vous avez déjà signalé ce contenu.");
        }

        var report = TripReport.Create(c.TargetType, c.TargetId, me.Id, c.Reason, clock.GetUtcNow());
        int count;
        switch (c.TargetType)
        {
            case VoyageReportTarget.Trip:
                var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == c.TargetId && t.HiddenReason != TripHiddenReason.Admin, ct)
                           ?? throw new NotFoundException("Trajet");
                NotOwn(trip.TravelerId);
                trip.AddReport();
                count = trip.ReportCount;
                break;
            case VoyageReportTarget.Booking:
                var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == c.TargetId && (b.SenderId == me.Id || b.TravelerId == me.Id), ct)
                              ?? throw new NotFoundException("Réservation");
                count = await db.Reports.CountAsync(r => r.TargetType == VoyageReportTarget.Booking && r.TargetId == booking.Id, ct) + 1;
                break;
            default:
                var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == c.TargetId && !r.IsHidden, ct) ?? throw new NotFoundException("Avis");
                NotOwn(review.AuthorId);
                review.AddReport();
                count = review.ReportCount;
                break;
        }

        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);
        return new CountDto(count);
    }

    private void NotOwn(Guid ownerId)
    {
        if (ownerId == me.Id)
        {
            throw new DomainException("Vous ne pouvez pas signaler votre propre contenu.");
        }
    }

    /// <summary>Réservation visible de l'expéditeur et du voyageur seulement.</summary>
    private async Task<(Trip Trip, Booking Booking)> LoadAsync(Guid bookingId, CancellationToken ct)
    {
        var tripId = await db.Bookings.Where(b => b.Id == bookingId && (b.SenderId == me.Id || b.TravelerId == me.Id)).Select(b => (Guid?)b.TripId)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Réservation");
        var trip = await db.Trips.Include(t => t.Bookings).FirstAsync(t => t.Id == tripId, ct);
        return (trip, trip.Find(bookingId));
    }

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

    private async Task<string> NameAsync(Guid userId, CancellationToken ct) =>
        VoyagesData.ShortName((await identity.GetNamesAsync([userId], ct)).GetValueOrDefault(userId));

    private async Task<BookingDto> DescribeAsync(Booking booking, Trip trip, CancellationToken ct) =>
        (await BookingViews.DescribeAsync(db, identity, me.Id, [booking], [trip], ct))[0];
}

public static class BookingViews
{
    public static async Task<List<BookingDto>> DescribeAsync(IVoyagesDb db, IIdentityModule identity, Guid me, IReadOnlyList<Booking> bookings,
        IReadOnlyList<Trip> trips, CancellationToken ct)
    {
        if (bookings.Count == 0)
        {
            return [];
        }

        var tripById = trips.ToDictionary(t => t.Id);
        var people = await VoyagesData.PeopleAsync(identity, bookings.SelectMany(b => new[] { b.SenderId, b.TravelerId }), ct);
        var ids = bookings.Select(b => b.Id).ToList();
        var myReviews = await db.Reviews.AsNoTracking().Where(r => ids.Contains(r.BookingId) && r.AuthorId == me)
            .ToDictionaryAsync(r => r.BookingId, r => r.Id, ct);
        return bookings.Select(b =>
        {
            var trip = tripById[b.TripId];
            var reviewId = myReviews.TryGetValue(b.Id, out var id) ? id : (Guid?)null;
            var iAmTraveler = b.TravelerId == me;
            return new BookingDto(b.Id, b.TripId, trip.FromCity, trip.ToCity, trip.DepartureDate, trip.Status, VoyagesData.Person(people, b.SenderId),
                VoyagesData.Person(people, b.TravelerId), iAmTraveler, b.WeightKg, b.Category, b.Description, b.Status, b.DeclineReason, b.ConversationId,
                b.CodeIssuedAt is not null && b.Status == BookingStatus.Accepted, b.CodeLocked,
                Math.Max(0, VoyageRules.DeliveryCodeAttempts - b.DeliveryAttempts), b.CreatedAt, b.RespondedAt, b.DeliveredAt, b.DisputeReason,
                b.ResolutionNote, b.Status == BookingStatus.Delivered && reviewId is null, reviewId);
        }).ToList();
    }
}
