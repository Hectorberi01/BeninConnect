using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Services.Domain;

public sealed record OfferingDetails(
    Guid CategoryId, string Title, string? Description, PriceType PriceType, decimal? PriceAmount, int? DurationMinutes, IReadOnlyList<string> Images,
    bool IsActive);

/// <summary>Prestation proposée par un prestataire (portfolio de 8 photos au plus).</summary>
public sealed class ServiceOffering
{
    private ServiceOffering()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public PriceType PriceType { get; private set; }
    public decimal? PriceAmount { get; private set; }
    public string Currency { get; private set; } = "EUR";
    public int? DurationMinutes { get; private set; }
    public List<string> Images { get; private set; } = [];
    public bool IsActive { get; private set; }
    public HiddenReason HiddenReason { get; private set; }
    public string? AlertKeyword { get; private set; }
    public string? ModerationNote { get; private set; }
    public int ReportCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsHidden => HiddenReason != HiddenReason.None;

    public bool IsListed => IsActive && !IsHidden;

    public bool NeedsReview => HiddenReason is HiddenReason.AlertKeyword or HiddenReason.Reports;

    public static ServiceOffering Create(Guid providerId, OfferingDetails details, string currency, DateTimeOffset now)
    {
        var offering = new ServiceOffering { Id = Guid.CreateVersion7(now), ProviderId = providerId, CreatedAt = now };
        offering.Update(details, currency, now);
        return offering;
    }

    public void Update(OfferingDetails d, string currency, DateTimeOffset now)
    {
        var title = (d.Title ?? string.Empty).Trim();
        if (title.Length is < 3 or > 100)
        {
            throw new DomainException("Titre de l'offre : 3 à 100 caractères.");
        }

        var description = (d.Description ?? string.Empty).Trim();
        if (description.Length > 3000)
        {
            throw new DomainException("Description : 3 000 caractères au plus.");
        }

        if (d.PriceType != PriceType.Quote && d.PriceAmount is not > 0)
        {
            throw new DomainException("Indiquez un prix, ou choisissez « Sur devis ».");
        }

        if (d.PriceAmount is > 1_000_000_000)
        {
            throw new DomainException("Prix trop élevé.");
        }

        if (d.DurationMinutes is < 5 or > 24 * 60 * 30)
        {
            throw new DomainException("Durée invalide.");
        }

        var images = d.Images.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToList();
        if (images.Count > ServiceRules.MaxImages)
        {
            throw new DomainException($"{ServiceRules.MaxImages} photos au plus par offre.");
        }

        CategoryId = d.CategoryId;
        Title = title;
        Description = description;
        PriceType = d.PriceType;
        PriceAmount = d.PriceType == PriceType.Quote ? null : decimal.Round(d.PriceAmount!.Value, 2);
        Currency = currency;
        DurationMinutes = d.DurationMinutes;
        Images = images;
        IsActive = d.IsActive;
        UpdatedAt = now;
    }

    /// <summary>Contenu modifié : s'il était masqué par un mot-clé, nouvelle vérification au prochain filtrage.</summary>
    public void ClearAlert()
    {
        if (HiddenReason == HiddenReason.AlertKeyword)
        {
            (HiddenReason, AlertKeyword) = (HiddenReason.None, null);
        }
    }

    public void FlagAlert(string keyword) => (HiddenReason, AlertKeyword) = (HiddenReason.AlertKeyword, ServiceRules.Clean(keyword, 100));

    public bool AddReport(int threshold)
    {
        ReportCount++;
        if (IsHidden || ReportCount < Math.Max(1, threshold))
        {
            return false;
        }

        HiddenReason = HiddenReason.Reports;
        return true;
    }

    public void Hide(string reason) =>
        (HiddenReason, ModerationNote) = (HiddenReason.Admin, ServiceRules.Clean(reason, 500));

    public void Show() => (HiddenReason, AlertKeyword, ModerationNote, ReportCount) = (HiddenReason.None, null, null, 0);
}

/// <summary>Demande de contact ou de devis d'un membre vers un prestataire. Aucun paiement dans l'application.</summary>
public sealed class ServiceRequest
{
    private ServiceRequest()
    {
    }

    public Guid Id { get; private set; }
    public Guid OfferingId { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid ProviderUserId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public DateOnly? PreferredDate { get; private set; }

    /// <summary>Lieu au niveau de la ville seulement.</summary>
    public string? City { get; private set; }

    public RequestStatus Status { get; private set; }
    public Guid? ConversationId { get; private set; }
    public string? DeclineReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ViewedAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? CompletedBy { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset? ReviewReminderSentAt { get; private set; }

    public static ServiceRequest Create(ServiceOffering offering, ProviderProfile provider, Guid clientId, string message, DateOnly? preferredDate,
        string? city, DateTimeOffset now)
    {
        if (provider.UserId == clientId)
        {
            throw new DomainException("Vous ne pouvez pas demander vos propres services.");
        }

        if (!provider.IsSearchable || !offering.IsListed || offering.ProviderId != provider.Id)
        {
            throw new DomainException("Cette offre n'est plus disponible.");
        }

        var text = (message ?? string.Empty).Trim();
        if (text.Length is < 10 or > 2000)
        {
            throw new DomainException("Votre message : 10 à 2 000 caractères.");
        }

        if (preferredDate is { } date && date < DateOnly.FromDateTime(now.UtcDateTime))
        {
            throw new DomainException("La date souhaitée est passée.");
        }

        return new ServiceRequest
        {
            Id = Guid.CreateVersion7(now),
            OfferingId = offering.Id,
            ProviderId = provider.Id,
            ProviderUserId = provider.UserId,
            ClientId = clientId,
            Message = text,
            PreferredDate = preferredDate,
            City = ServiceRules.CleanOrNull(city, 80),
            Status = RequestStatus.Sent,
            CreatedAt = now
        };
    }

    public void AttachConversation(Guid conversationId) => ConversationId = conversationId;

    public bool IsParticipant(Guid userId) => userId == ClientId || userId == ProviderUserId;

    /// <summary>Ouverte par le prestataire.</summary>
    public void MarkViewed(DateTimeOffset now)
    {
        if (Status == RequestStatus.Sent)
        {
            (Status, ViewedAt) = (RequestStatus.Viewed, now);
        }
    }

    public void Accept(Guid userId, DateTimeOffset now)
    {
        RequireProvider(userId);
        Require(RequestStatus.Sent, RequestStatus.Viewed);
        (Status, RespondedAt) = (RequestStatus.Accepted, now);
        ViewedAt ??= now;
    }

    public void Decline(Guid userId, string? reason, DateTimeOffset now)
    {
        RequireProvider(userId);
        Require(RequestStatus.Sent, RequestStatus.Viewed);
        (Status, RespondedAt, DeclineReason) = (RequestStatus.Declined, now, ServiceRules.CleanOrNull(reason, 500));
        ViewedAt ??= now;
    }

    /// <summary>Prestation réalisée, déclarée par le client ou le prestataire : débloque l'avis.</summary>
    public void Complete(Guid userId, DateTimeOffset now)
    {
        if (!IsParticipant(userId))
        {
            throw new ForbiddenException("Cette demande ne vous concerne pas.");
        }

        Require(RequestStatus.Accepted);
        (Status, CompletedAt, CompletedBy) = (RequestStatus.Completed, now, userId);
    }

    /// <summary>Annulation par le client avant la réalisation.</summary>
    public void Cancel(Guid userId, DateTimeOffset now)
    {
        if (userId != ClientId)
        {
            throw new ForbiddenException("Seul le client peut annuler sa demande.");
        }

        Require(RequestStatus.Sent, RequestStatus.Viewed, RequestStatus.Accepted);
        (Status, CancelledAt) = (RequestStatus.Cancelled, now);
    }

    public void MarkReminded(DateTimeOffset now) => ReviewReminderSentAt = now;

    private void RequireProvider(Guid userId)
    {
        if (userId != ProviderUserId)
        {
            throw new ForbiddenException("Seul le prestataire peut répondre à cette demande.");
        }
    }

    private void Require(params RequestStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException(Status switch
            {
                RequestStatus.Declined => "Cette demande a été refusée.",
                RequestStatus.Cancelled => "Cette demande a été annulée.",
                RequestStatus.Completed => "Cette prestation est déjà terminée.",
                RequestStatus.Accepted => "Cette demande a déjà été acceptée.",
                _ => "Le prestataire n'a pas encore accepté cette demande."
            });
        }
    }
}
