using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Voyages.Domain;

/// <summary>Avis après une livraison : un par côté et par réservation.</summary>
public sealed class TripReview
{
    private TripReview()
    {
    }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid TripId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid TargetId { get; private set; }

    /// <summary>Vrai : avis de l'expéditeur sur le voyageur.</summary>
    public bool AboutTraveler { get; private set; }

    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsHidden { get; private set; }
    public string? HiddenReason { get; private set; }
    public int ReportCount { get; private set; }

    public static TripReview Create(Booking booking, Guid authorId, int rating, string? comment, DateTimeOffset now)
    {
        if (authorId != booking.SenderId && authorId != booking.TravelerId)
        {
            throw new ForbiddenException("Seuls l'expéditeur et le voyageur notent cette livraison.");
        }

        if (booking.Status != BookingStatus.Delivered)
        {
            throw new DomainException("Les avis s'ouvrent une fois la livraison confirmée.");
        }

        if (rating is < 1 or > 5)
        {
            throw new DomainException("Note : de 1 à 5.");
        }

        var aboutTraveler = authorId == booking.SenderId;
        return new TripReview
        {
            Id = Guid.CreateVersion7(now),
            BookingId = booking.Id,
            TripId = booking.TripId,
            AuthorId = authorId,
            TargetId = aboutTraveler ? booking.TravelerId : booking.SenderId,
            AboutTraveler = aboutTraveler,
            Rating = rating,
            Comment = VoyageRules.OptionalText(comment, 500, "Commentaire"),
            CreatedAt = now
        };
    }

    public void AddReport()
    {
        ReportCount++;
        if (ReportCount >= VoyageRules.ReportThreshold)
        {
            IsHidden = true;
            HiddenReason ??= "Signalements";
        }
    }

    public void Hide(string reason)
    {
        IsHidden = true;
        HiddenReason = VoyageRules.Reason(reason);
    }

    public void Show()
    {
        IsHidden = false;
        HiddenReason = null;
        ReportCount = 0;
    }
}

/// <summary>Alerte de trajet : le membre est prévenu dès qu'un trajet correspondant est publié.</summary>
public sealed class RouteAlert
{
    private RouteAlert()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string FromCountry { get; private set; } = string.Empty;
    public string? FromCity { get; private set; }
    public string? FromCityKey { get; private set; }
    public string ToCountry { get; private set; } = string.Empty;
    public string? ToCity { get; private set; }
    public string? ToCityKey { get; private set; }
    public DateOnly? After { get; private set; }
    public DateOnly? Before { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastNotifiedAt { get; private set; }

    public static RouteAlert Create(Guid userId, string fromCountry, string? fromCity, string toCountry, string? toCity, DateOnly? after, DateOnly? before,
        DateTimeOffset now)
    {
        if (after is { } a && before is { } b && b < a)
        {
            throw new DomainException("Période invalide : la fin précède le début.");
        }

        if (before is { } end && end < Trip.Today(now))
        {
            throw new DomainException("Période déjà passée.");
        }

        var from = string.IsNullOrWhiteSpace(fromCity) ? null : VoyageRules.City(fromCity, "Départ");
        var to = string.IsNullOrWhiteSpace(toCity) ? null : VoyageRules.City(toCity, "Arrivée");
        return new RouteAlert
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            FromCountry = VoyageRules.Country(fromCountry, "Départ"),
            FromCity = from,
            FromCityKey = from is null ? null : VoyageRules.CityKey(from),
            ToCountry = VoyageRules.Country(toCountry, "Arrivée"),
            ToCity = to,
            ToCityKey = to is null ? null : VoyageRules.CityKey(to),
            After = after,
            Before = before,
            CreatedAt = now
        };
    }

    public bool Matches(Trip trip) =>
        trip.FromCountry == FromCountry && trip.ToCountry == ToCountry
        && (FromCityKey is null || FromCityKey == trip.FromCityKey) && (ToCityKey is null || ToCityKey == trip.ToCityKey)
        && (After is null || trip.DepartureDate >= After) && (Before is null || trip.DepartureDate <= Before);

    public void MarkNotified(DateTimeOffset now) => LastNotifiedAt = now;
}

/// <summary>Signalement d'un trajet, d'une réservation ou d'un avis (un par membre et par cible).</summary>
public sealed class TripReport
{
    private TripReport()
    {
    }

    public Guid Id { get; private set; }
    public VoyageReportTarget TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool Handled { get; private set; }
    public string? Decision { get; private set; }

    public static TripReport Create(VoyageReportTarget target, Guid targetId, Guid authorId, string reason, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            TargetType = target,
            TargetId = targetId,
            AuthorId = authorId,
            Reason = VoyageRules.Reason(reason),
            CreatedAt = now
        };

    public void Close(string decision)
    {
        Handled = true;
        Decision = decision;
    }
}
