using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Voyages.Domain;

/// <summary>Saisie d'un trajet par le voyageur.</summary>
public sealed record TripDetails(
    string FromCountry, string FromCity, string ToCountry, string ToCity, DateOnly DepartureDate, decimal CapacityKg, decimal? PricePerKg,
    string? Currency, IReadOnlyList<ParcelCategory> AcceptedCategories, string? DropOffZones, string? Notes);

/// <summary>Trajet publié par un voyageur : kilos disponibles pour les colis d'autres membres. Agrégat des réservations.</summary>
public sealed class Trip
{
    private Trip()
    {
    }

    public Guid Id { get; private set; }
    public Guid TravelerId { get; private set; }
    public string FromCountry { get; private set; } = string.Empty;
    public string FromCity { get; private set; } = string.Empty;
    public string FromCityKey { get; private set; } = string.Empty;
    public string ToCountry { get; private set; } = string.Empty;
    public string ToCity { get; private set; } = string.Empty;
    public string ToCityKey { get; private set; } = string.Empty;
    public DateOnly DepartureDate { get; private set; }
    public decimal CapacityKg { get; private set; }

    /// <summary>Indicatif : le paiement se fait hors de l'application.</summary>
    public decimal? PricePerKg { get; private set; }

    public string Currency { get; private set; } = "EUR";
    public List<ParcelCategory> AcceptedCategories { get; private set; } = [];
    public string? DropOffZones { get; private set; }
    public string? Notes { get; private set; }
    public TripStatus Status { get; private set; }
    public TripHiddenReason HiddenReason { get; private set; }
    public string? HiddenNote { get; private set; }
    public int ReportCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset? ReminderSentAt { get; private set; }

    /// <summary>Jeton de concurrence (xmin) : deux acceptations simultanées ne dépassent pas la capacité.</summary>
    public uint Version { get; private set; }

    public List<Booking> Bookings { get; private set; } = [];

    public decimal ReservedKg => Bookings.Where(b => b.HoldsCapacity).Sum(b => b.WeightKg);

    public decimal RemainingKg => Math.Max(0, CapacityKg - ReservedKg);

    public bool IsVisible => HiddenReason == TripHiddenReason.None;

    public bool IsBookable(DateOnly today) => Status == TripStatus.Open && IsVisible && DepartureDate >= today;

    public static Trip Create(Guid travelerId, TripDetails details, DateTimeOffset now)
    {
        var trip = new Trip { Id = Guid.CreateVersion7(now), TravelerId = travelerId, Status = TripStatus.Open, CreatedAt = now };
        trip.Apply(details, now);
        return trip;
    }

    public void Update(Guid travelerId, TripDetails details, DateTimeOffset now)
    {
        Own(travelerId);
        if (Status is not (TripStatus.Open or TripStatus.Full))
        {
            throw new DomainException("Ce trajet ne peut plus être modifié.");
        }

        if (Bookings.Any(b => b.Status is BookingStatus.Accepted or BookingStatus.Delivered or BookingStatus.Disputed))
        {
            throw new DomainException("Une réservation est déjà acceptée : annulez le trajet ou contactez vos expéditeurs.");
        }

        Apply(details, now);
        Status = TripStatus.Open;
    }

    /// <summary>Annulation par le voyageur : les réservations en cours sont annulées (renvoyées pour prévenir les expéditeurs).</summary>
    public IReadOnlyList<Booking> Cancel(Guid travelerId, DateTimeOffset now)
    {
        Own(travelerId);
        if (Status is not (TripStatus.Open or TripStatus.Full))
        {
            throw new DomainException("Ce trajet ne peut plus être annulé.");
        }

        Status = TripStatus.Cancelled;
        CancelledAt = now;
        Touch(now);
        var affected = Bookings.Where(b => b.Status is BookingStatus.Pending or BookingStatus.Accepted).ToList();
        foreach (var booking in affected)
        {
            booking.CancelByTrip(TravelerId, now);
        }

        return affected;
    }

    public Booking RequestBooking(Guid senderId, decimal kg, ParcelCategory category, string description, bool certified, DateTimeOffset now)
    {
        if (senderId == TravelerId)
        {
            throw new DomainException("Vous ne pouvez pas réserver votre propre trajet.");
        }

        if (!IsBookable(Today(now)))
        {
            throw new DomainException("Ce trajet n'accepte plus de réservations.");
        }

        if (!AcceptedCategories.Contains(category))
        {
            throw new DomainException("Le voyageur n'accepte pas cette catégorie.");
        }

        if (Bookings.Any(b => b.SenderId == senderId && b.IsActive))
        {
            throw new ConflictException("Vous avez déjà une réservation sur ce trajet.");
        }

        if (kg > RemainingKg)
        {
            throw new DomainException($"Il reste {RemainingKg:0.#} kg sur ce trajet.");
        }

        // La capacité est vérifiée de nouveau à l'acceptation
        var booking = Booking.Request(this, senderId, kg, category, description, certified, now);
        Bookings.Add(booking);
        Touch(now);
        return booking;
    }

    public Booking Accept(Guid bookingId, Guid travelerId, DateTimeOffset now)
    {
        Own(travelerId);
        var booking = Find(bookingId);
        if (Status is not (TripStatus.Open or TripStatus.Full) || DepartureDate < Today(now))
        {
            throw new DomainException("Ce trajet n'accepte plus de réservations.");
        }

        if (ReservedKg + booking.WeightKg > CapacityKg)
        {
            throw new DomainException($"Plus assez de kilos disponibles : il reste {RemainingKg:0.#} kg.");
        }

        booking.Accept(now);
        if (ReservedKg >= CapacityKg)
        {
            Status = TripStatus.Full;
        }

        Touch(now);
        return booking;
    }

    public Booking Decline(Guid bookingId, Guid travelerId, string? reason, DateTimeOffset now)
    {
        Own(travelerId);
        var booking = Find(bookingId);
        booking.Decline(VoyageRules.OptionalText(reason, 300, "Motif"), now);
        Touch(now);
        return booking;
    }

    /// <summary>Annulation par l'expéditeur ou le voyageur ; un trajet complet redevient ouvert avant le départ.</summary>
    public Booking CancelBooking(Guid bookingId, Guid userId, DateTimeOffset now)
    {
        var booking = Find(bookingId);
        if (userId != booking.SenderId && userId != TravelerId)
        {
            throw new ForbiddenException("Seuls l'expéditeur et le voyageur peuvent annuler cette réservation.");
        }

        booking.Cancel(userId, now);
        if (Status == TripStatus.Full && ReservedKg < CapacityKg && DepartureDate >= Today(now))
        {
            Status = TripStatus.Open;
        }

        Touch(now);
        return booking;
    }

    /// <summary>Lendemain du départ : trajet parti, demandes restées sans réponse annulées (renvoyées).</summary>
    public IReadOnlyList<Booking> MarkDeparted(DateTimeOffset now)
    {
        if (Status is not (TripStatus.Open or TripStatus.Full) || DepartureDate >= Today(now))
        {
            return [];
        }

        Status = TripStatus.Departed;
        Touch(now);
        var expired = Bookings.Where(b => b.Status == BookingStatus.Pending).ToList();
        foreach (var booking in expired)
        {
            booking.Expire(now);
        }

        return expired;
    }

    public void MarkReminderSent(DateTimeOffset now) => ReminderSentAt = now;

    /// <summary>Signalements : masqué automatiquement au seuil, en attendant la revue.</summary>
    public void AddReport()
    {
        ReportCount++;
        if (ReportCount >= VoyageRules.ReportThreshold && HiddenReason == TripHiddenReason.None)
        {
            HiddenReason = TripHiddenReason.Reports;
        }
    }

    public void Hide(TripHiddenReason reason, string? note, DateTimeOffset now)
    {
        HiddenReason = reason;
        HiddenNote = note;
        Touch(now);
    }

    public void Show(DateTimeOffset now)
    {
        HiddenReason = TripHiddenReason.None;
        HiddenNote = null;
        ReportCount = 0;
        Touch(now);
    }

    public Booking Find(Guid bookingId) => Bookings.FirstOrDefault(b => b.Id == bookingId) ?? throw new NotFoundException("Réservation");

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    private void Own(Guid travelerId)
    {
        if (travelerId != TravelerId)
        {
            throw new ForbiddenException("Seul le voyageur peut gérer ce trajet.");
        }
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;

    private void Apply(TripDetails d, DateTimeOffset now)
    {
        FromCountry = VoyageRules.Country(d.FromCountry, "Départ");
        FromCity = VoyageRules.City(d.FromCity, "Départ");
        ToCountry = VoyageRules.Country(d.ToCountry, "Arrivée");
        ToCity = VoyageRules.City(d.ToCity, "Arrivée");
        FromCityKey = VoyageRules.CityKey(FromCity);
        ToCityKey = VoyageRules.CityKey(ToCity);
        if (FromCountry == ToCountry && FromCityKey == ToCityKey)
        {
            throw new DomainException("Les villes de départ et d'arrivée doivent être différentes.");
        }

        var today = Today(now);
        if (d.DepartureDate < today || d.DepartureDate > today.AddDays(VoyageRules.MaxDaysAhead))
        {
            throw new DomainException($"Date de départ : d'aujourd'hui à {VoyageRules.MaxDaysAhead} jours au plus.");
        }

        if (d.CapacityKg is < VoyageRules.MinCapacityKg or > VoyageRules.MaxCapacityKg)
        {
            throw new DomainException($"Kilos disponibles : de {VoyageRules.MinCapacityKg} à {VoyageRules.MaxCapacityKg} kg.");
        }

        if (d.CapacityKg < ReservedKg)
        {
            throw new DomainException($"{ReservedKg:0.#} kg sont déjà réservés.");
        }

        var currency = string.IsNullOrWhiteSpace(d.Currency) ? "EUR" : d.Currency.Trim().ToUpperInvariant();
        if (!VoyageRules.Currencies.Contains(currency))
        {
            throw new DomainException("Devise : EUR ou XOF.");
        }

        if (d.PricePerKg is { } price && (price < 0 || price > VoyageRules.MaxPricePerKg(currency)))
        {
            throw new DomainException($"Prix au kilo : de 0 à {VoyageRules.MaxPricePerKg(currency):0} {currency}.");
        }

        var categories = (d.AcceptedCategories ?? []).Distinct().ToList();
        if (categories.Count == 0)
        {
            throw new DomainException("Choisissez au moins une catégorie acceptée.");
        }

        DepartureDate = d.DepartureDate;
        CapacityKg = decimal.Round(d.CapacityKg, 1);
        PricePerKg = d.PricePerKg is { } p ? decimal.Round(p, 2) : null;
        Currency = currency;
        AcceptedCategories = categories;
        DropOffZones = VoyageRules.OptionalText(d.DropOffZones, 300, "Zones de remise");
        Notes = VoyageRules.OptionalText(d.Notes, 1000, "Informations");
        Touch(now);
    }
}
