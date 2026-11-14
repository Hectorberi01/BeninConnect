using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Voyages.Domain;

/// <summary>Kilos réservés par un expéditeur sur un trajet. La livraison est prouvée par un code à 6 chiffres donné par le destinataire.</summary>
public sealed class Booking
{
    private Booking()
    {
    }

    public Guid Id { get; private set; }
    public Guid TripId { get; private set; }
    public Guid TravelerId { get; private set; }
    public Guid SenderId { get; private set; }
    public decimal WeightKg { get; private set; }
    public ParcelCategory Category { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset CertifiedAt { get; private set; }
    public BookingStatus Status { get; private set; }

    /// <summary>Code de remise haché ; le code en clair n'est montré qu'une fois à l'expéditeur.</summary>
    public string? DeliveryCodeHash { get; private set; }

    public int DeliveryAttempts { get; private set; }
    public DateTimeOffset? CodeIssuedAt { get; private set; }
    public Guid? ConversationId { get; private set; }
    public string? DeclineReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public BookingStatus? StatusBeforeDispute { get; private set; }
    public string? DisputeReason { get; private set; }
    public Guid? DisputedBy { get; private set; }
    public DateTimeOffset? DisputedAt { get; private set; }
    public string? ResolutionNote { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>En cours : bloque une seconde réservation du même expéditeur sur le trajet.</summary>
    public bool IsActive => Status is BookingStatus.Pending or BookingStatus.Accepted or BookingStatus.Disputed;

    /// <summary>Kilos comptés dans la capacité du trajet.</summary>
    public bool HoldsCapacity => Status is BookingStatus.Accepted or BookingStatus.Delivered or BookingStatus.Disputed;

    public bool CodeLocked => DeliveryAttempts >= VoyageRules.DeliveryCodeAttempts;

    internal static Booking Request(Trip trip, Guid senderId, decimal kg, ParcelCategory category, string description, bool certified, DateTimeOffset now)
    {
        if (!certified)
        {
            throw new DomainException("Certifiez que le colis ne contient aucun objet interdit.");
        }

        if (kg is < VoyageRules.MinBookingKg or > VoyageRules.MaxBookingKg)
        {
            throw new DomainException($"Poids : de {VoyageRules.MinBookingKg} à {VoyageRules.MaxBookingKg} kg.");
        }

        var text = (description ?? string.Empty).Trim();
        if (text.Length is < 10 or > 500)
        {
            throw new DomainException("Décrivez le contenu : 10 à 500 caractères.");
        }

        return new Booking
        {
            Id = Guid.CreateVersion7(now),
            TripId = trip.Id,
            TravelerId = trip.TravelerId,
            SenderId = senderId,
            WeightKg = decimal.Round(kg, 1),
            Category = category,
            Description = text,
            CertifiedAt = now,
            Status = BookingStatus.Pending,
            CreatedAt = now
        };
    }

    internal void Accept(DateTimeOffset now)
    {
        Require(BookingStatus.Pending, "Cette demande a déjà reçu une réponse.");
        Status = BookingStatus.Accepted;
        RespondedAt = now;
    }

    internal void Decline(string? reason, DateTimeOffset now)
    {
        Require(BookingStatus.Pending, "Cette demande a déjà reçu une réponse.");
        Status = BookingStatus.Declined;
        DeclineReason = reason;
        RespondedAt = now;
    }

    internal void Cancel(Guid userId, DateTimeOffset now)
    {
        if (Status is not (BookingStatus.Pending or BookingStatus.Accepted))
        {
            throw new DomainException("Cette réservation ne peut plus être annulée.");
        }

        Status = BookingStatus.Cancelled;
        CancelledAt = now;
        CancelledBy = userId;
    }

    internal void CancelByTrip(Guid travelerId, DateTimeOffset now)
    {
        Status = BookingStatus.Cancelled;
        CancelledAt = now;
        CancelledBy = travelerId;
    }

    internal void Expire(DateTimeOffset now)
    {
        Status = BookingStatus.Cancelled;
        CancelledAt = now;
        DeclineReason = "Sans réponse avant le départ.";
    }

    public void AttachConversation(Guid conversationId) => ConversationId = conversationId;

    /// <summary>L'expéditeur obtient un nouveau code (l'ancien ne marche plus, les essais repartent de zéro).</summary>
    public void SetDeliveryCode(Guid senderId, string codeHash, DateTimeOffset now)
    {
        if (senderId != SenderId)
        {
            throw new ForbiddenException("Seul l'expéditeur obtient le code de remise.");
        }

        Require(BookingStatus.Accepted, "Le code est disponible une fois la réservation acceptée.");
        DeliveryCodeHash = codeHash;
        DeliveryAttempts = 0;
        CodeIssuedAt = now;
    }

    /// <summary>Le voyageur saisit le code donné par le destinataire. Faux : essai compté (à enregistrer avant de répondre).</summary>
    public bool TryDeliver(Guid travelerId, string code, Func<string, string, bool> verify, DateTimeOffset now)
    {
        if (travelerId != TravelerId)
        {
            throw new ForbiddenException("Seul le voyageur confirme la livraison.");
        }

        Require(BookingStatus.Accepted, "Cette réservation n'est pas en attente de livraison.");
        if (DeliveryCodeHash is null)
        {
            throw new DomainException("L'expéditeur n'a pas encore généré le code de remise.");
        }

        if (CodeLocked)
        {
            throw new DomainException("Trop d'essais : l'expéditeur doit générer un nouveau code.");
        }

        var value = (code ?? string.Empty).Trim();
        if (value.Length != 6 || !value.All(char.IsAsciiDigit) || !verify(DeliveryCodeHash, value))
        {
            DeliveryAttempts++;
            return false;
        }

        Status = BookingStatus.Delivered;
        DeliveredAt = now;
        DeliveryCodeHash = null;
        return true;
    }

    public void Dispute(Guid userId, string? reason, DateTimeOffset now)
    {
        if (userId != SenderId && userId != TravelerId)
        {
            throw new ForbiddenException("Seuls l'expéditeur et le voyageur peuvent ouvrir un litige.");
        }

        if (Status is not (BookingStatus.Accepted or BookingStatus.Delivered))
        {
            throw new DomainException("Un litige s'ouvre sur une réservation acceptée ou livrée.");
        }

        StatusBeforeDispute = Status;
        Status = BookingStatus.Disputed;
        DisputeReason = VoyageRules.Reason(reason);
        DisputedBy = userId;
        DisputedAt = now;
    }

    /// <summary>Décision de la modération : livraison confirmée ou réservation annulée.</summary>
    public void Resolve(DisputeOutcome outcome, string note, DateTimeOffset now)
    {
        Require(BookingStatus.Disputed, "Aucun litige en cours sur cette réservation.");
        Status = outcome == DisputeOutcome.Delivered ? BookingStatus.Delivered : BookingStatus.Cancelled;
        if (outcome == DisputeOutcome.Delivered)
        {
            DeliveredAt ??= now;
        }
        else
        {
            CancelledAt = now;
        }

        ResolutionNote = VoyageRules.Reason(note);
        ResolvedAt = now;
        DeliveryCodeHash = null;
    }

    private void Require(BookingStatus status, string message)
    {
        if (Status != status)
        {
            throw new DomainException(message);
        }
    }
}
