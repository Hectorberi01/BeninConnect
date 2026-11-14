using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Voyages.Domain;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Voyages.Application;

/// <summary>Tâche horaire : rappels de la veille, trajets partis, trajets des voyageurs suspendus masqués.</summary>
public sealed class TripLifecycle(IVoyagesDb db, IIdentityModule identity, IEventBus bus, TimeProvider clock)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var changes = await SendRemindersAsync(ct);
        changes += await MarkDepartedAsync(ct);
        changes += await SyncSuspendedAsync(ct);
        return changes;
    }

    /// <summary>Veille du départ : le voyageur et ses expéditeurs acceptés sont prévenus (une seule fois).</summary>
    public async Task<int> SendRemindersAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var tomorrow = Trip.Today(now).AddDays(1);
        var trips = await db.Trips.Include(t => t.Bookings)
            .Where(t => (t.Status == TripStatus.Open || t.Status == TripStatus.Full) && t.DepartureDate == tomorrow && t.ReminderSentAt == null)
            .ToListAsync(ct);
        foreach (var trip in trips)
        {
            trip.MarkReminderSent(now);
            var accepted = trip.Bookings.Where(b => b.Status == BookingStatus.Accepted).ToList();
            await VoyagesData.NotifyAsync(bus, trip.TravelerId, "Départ demain",
                accepted.Count == 0
                    ? $"Votre voyage {VoyagesData.Route(trip)} part demain."
                    : $"{accepted.Count} colis à récupérer pour {VoyagesData.Route(trip)}. Ouvrez et vérifiez chaque colis à la remise.", null, ct);
            foreach (var booking in accepted)
            {
                await VoyagesData.NotifyAsync(bus, booking.SenderId, "Le voyageur part demain",
                    $"Remettez votre colis ouvert pour {VoyagesData.Route(trip)} et transmettez le code de remise à votre destinataire.", booking.ConversationId, ct);
            }
        }

        await db.SaveChangesAsync(ct);
        return trips.Count;
    }

    /// <summary>Lendemain du départ : trajet parti, demandes sans réponse annulées.</summary>
    public async Task<int> MarkDepartedAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = Trip.Today(now);
        var trips = await db.Trips.Include(t => t.Bookings)
            .Where(t => (t.Status == TripStatus.Open || t.Status == TripStatus.Full) && t.DepartureDate < today).ToListAsync(ct);
        var expired = trips.SelectMany(t => t.MarkDeparted(now).Select(b => (Trip: t, Booking: b))).ToList();
        await db.SaveChangesAsync(ct);
        foreach (var (trip, booking) in expired)
        {
            await VoyagesData.NotifyAsync(bus, booking.SenderId, "Demande expirée",
                $"Le voyageur n'a pas répondu avant son départ ({VoyagesData.Route(trip)}). Cherchez un autre trajet.", null, ct);
        }

        return trips.Count;
    }

    /// <summary>
    /// Trajets à venir des voyageurs suspendus masqués, rétablis à la levée de la suspension.
    /// Le contrat Identity n'expose la suspension que par l'export des membres : appelé une fois par heure seulement.
    /// </summary>
    public async Task<int> SyncSuspendedAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = Trip.Today(now);
        var trips = await db.Trips
            .Where(t => (t.Status == TripStatus.Open || t.Status == TripStatus.Full) && t.DepartureDate >= today
                        && (t.HiddenReason == TripHiddenReason.None || t.HiddenReason == TripHiddenReason.Suspended))
            .ToListAsync(ct);
        if (trips.Count == 0)
        {
            return 0;
        }

        var suspended = (await identity.GetExportRowsAsync(ct)).Where(m => m.IsSuspended).Select(m => m.Id).ToHashSet();
        var changed = 0;
        foreach (var trip in trips)
        {
            if (suspended.Contains(trip.TravelerId) && trip.HiddenReason == TripHiddenReason.None)
            {
                trip.Hide(TripHiddenReason.Suspended, "Compte suspendu", now);
                changed++;
            }
            else if (!suspended.Contains(trip.TravelerId) && trip.HiddenReason == TripHiddenReason.Suspended)
            {
                trip.Show(now);
                changed++;
            }
        }

        await db.SaveChangesAsync(ct);
        return changed;
    }
}

/// <summary>Compte supprimé : ses trajets, réservations, avis, alertes et signalements sont effacés ; l'autre partie est prévenue.</summary>
public sealed class DeleteMemberVoyages(IVoyagesDb db, IEventBus bus, TimeProvider clock) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var notices = new List<(Guid UserId, string Title, string Body)>();

        // Ses trajets : les expéditeurs en cours sont prévenus
        var trips = await db.Trips.Include(t => t.Bookings).Where(t => t.TravelerId == e.UserId).ToListAsync(ct);
        foreach (var trip in trips)
        {
            notices.AddRange(trip.Bookings.Where(b => b.IsActive).Select(b =>
                (b.SenderId, "Trajet annulé", $"Le voyageur a supprimé son compte : votre réservation sur {VoyagesData.Route(trip)} est annulée.")));
        }

        // Ses réservations sur les trajets des autres : les voyageurs en cours sont prévenus, les kilos libérés
        var sent = await db.Bookings.Where(b => b.SenderId == e.UserId && b.TravelerId != e.UserId).ToListAsync(ct);
        var otherTripIds = sent.Select(b => b.TripId).Distinct().ToList();
        var otherTrips = await db.Trips.Include(t => t.Bookings).Where(t => otherTripIds.Contains(t.Id)).ToListAsync(ct);
        foreach (var booking in sent.Where(b => b.IsActive))
        {
            var trip = otherTrips.First(t => t.Id == booking.TripId);
            if (booking.Status is BookingStatus.Pending or BookingStatus.Accepted)
            {
                trip.CancelBooking(booking.Id, e.UserId, now);
            }

            notices.Add((trip.TravelerId, "Réservation annulée", $"Un expéditeur a supprimé son compte : sa réservation sur {VoyagesData.Route(trip)} est annulée."));
        }

        await db.SaveChangesAsync(ct);

        var tripIds = trips.Select(t => t.Id).ToList();
        var bookingIds = trips.SelectMany(t => t.Bookings).Select(b => b.Id).Concat(sent.Select(b => b.Id)).ToList();
        var reviewIds = await db.Reviews.Where(r => bookingIds.Contains(r.BookingId) || r.AuthorId == e.UserId || r.TargetId == e.UserId)
            .Select(r => r.Id).ToListAsync(ct);
        await db.Reports.Where(r => r.AuthorId == e.UserId
                                    || (r.TargetType == VoyageReportTarget.Trip && tripIds.Contains(r.TargetId))
                                    || (r.TargetType == VoyageReportTarget.Booking && bookingIds.Contains(r.TargetId))
                                    || (r.TargetType == VoyageReportTarget.Review && reviewIds.Contains(r.TargetId))).ExecuteDeleteAsync(ct);
        await db.Reviews.Where(r => reviewIds.Contains(r.Id)).ExecuteDeleteAsync(ct);
        await db.Bookings.Where(b => bookingIds.Contains(b.Id)).ExecuteDeleteAsync(ct);
        await db.Trips.Where(t => tripIds.Contains(t.Id)).ExecuteDeleteAsync(ct);
        await db.Alerts.Where(a => a.UserId == e.UserId).ExecuteDeleteAsync(ct);

        foreach (var (userId, title, body) in notices.Where(n => n.UserId != e.UserId))
        {
            await VoyagesData.NotifyAsync(bus, userId, title, body, null, ct);
        }
    }
}
