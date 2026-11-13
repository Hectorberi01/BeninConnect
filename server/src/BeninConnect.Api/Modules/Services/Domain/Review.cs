using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Services.Domain;

/// <summary>Avis vérifié : seulement après une demande terminée, un par demande.</summary>
public sealed class Review
{
    private Review()
    {
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid AuthorId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? ProviderReply { get; private set; }
    public DateTimeOffset? RepliedAt { get; private set; }
    public ReviewStatus Status { get; private set; }
    public HiddenReason HiddenReason { get; private set; }

    /// <summary>Motif documenté de masquage (obligatoire : un avis négatif n'est jamais retiré sans motif).</summary>
    public string? ModerationNote { get; private set; }

    public int ReportCount { get; private set; }

    public bool NeedsReview => HiddenReason == HiddenReason.Reports;

    public static Review Create(ServiceRequest request, Guid authorId, int rating, string? comment, DateTimeOffset now)
    {
        if (authorId != request.ClientId)
        {
            throw new ForbiddenException(authorId == request.ProviderUserId
                ? "Vous ne pouvez pas noter vos propres services."
                : "Seul le client de cette demande peut laisser un avis.");
        }

        if (request.Status != RequestStatus.Completed)
        {
            throw new DomainException("Vous pourrez laisser un avis quand la prestation sera marquée comme terminée.");
        }

        var review = new Review
        {
            Id = Guid.CreateVersion7(now), RequestId = request.Id, ProviderId = request.ProviderId, AuthorId = authorId, CreatedAt = now,
            Status = ReviewStatus.Published
        };
        review.SetContent(rating, comment);
        return review;
    }

    /// <summary>Modifiable par son auteur pendant 7 jours.</summary>
    public void Edit(Guid authorId, int rating, string? comment, DateTimeOffset now)
    {
        if (authorId != AuthorId)
        {
            throw new ForbiddenException("Seul l'auteur peut modifier cet avis.");
        }

        if (now > CreatedAt.AddDays(ServiceRules.ReviewEditDays))
        {
            throw new DomainException($"Un avis n'est plus modifiable après {ServiceRules.ReviewEditDays} jours.");
        }

        SetContent(rating, comment);
        UpdatedAt = now;
    }

    /// <summary>Une seule réponse du prestataire.</summary>
    public void Reply(string reply, DateTimeOffset now)
    {
        if (ProviderReply is not null)
        {
            throw new DomainException("Vous avez déjà répondu à cet avis.");
        }

        var text = (reply ?? string.Empty).Trim();
        if (text.Length is < 2 or > 1000)
        {
            throw new DomainException("Votre réponse : 2 à 1 000 caractères.");
        }

        (ProviderReply, RepliedAt) = (text, now);
    }

    public bool AddReport(int threshold)
    {
        ReportCount++;
        if (Status == ReviewStatus.Hidden || ReportCount < Math.Max(1, threshold))
        {
            return false;
        }

        (Status, HiddenReason) = (ReviewStatus.Hidden, HiddenReason.Reports);
        return true;
    }

    public void Hide(string reason)
    {
        var note = (reason ?? string.Empty).Trim();
        if (note.Length < 10)
        {
            throw new DomainException("Motif de modération obligatoire (10 caractères au moins) : il reste attaché à l'avis.");
        }

        (Status, HiddenReason, ModerationNote) = (ReviewStatus.Hidden, HiddenReason.Admin, ServiceRules.Clean(note, 500));
    }

    public void Show() => (Status, HiddenReason, ModerationNote, ReportCount) = (ReviewStatus.Published, HiddenReason.None, null, 0);

    private void SetContent(int rating, string? comment)
    {
        if (rating is < 1 or > 5)
        {
            throw new DomainException("Note : de 1 à 5 étoiles.");
        }

        var text = ServiceRules.CleanOrNull(comment, 2000);
        Rating = rating;
        Comment = text;
    }
}

/// <summary>Signalement d'un profil, d'une offre ou d'un avis (un par membre et par cible).</summary>
public sealed class ServiceReport
{
    private ServiceReport()
    {
    }

    public Guid Id { get; private set; }
    public ReportTarget TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool Handled { get; private set; }
    public string? Decision { get; private set; }

    public static ServiceReport Create(ReportTarget target, Guid targetId, Guid authorId, string reason, DateTimeOffset now)
    {
        var text = (reason ?? string.Empty).Trim();
        if (text.Length is < 3 or > 500)
        {
            throw new DomainException("Motif du signalement : 3 à 500 caractères.");
        }

        return new ServiceReport { Id = Guid.CreateVersion7(now), TargetType = target, TargetId = targetId, AuthorId = authorId, Reason = text, CreatedAt = now };
    }

    public void Close(string decision) => (Handled, Decision) = (true, ServiceRules.Clean(decision, 200));
}

public sealed class ProviderFavorite
{
    private ProviderFavorite()
    {
    }

    public Guid UserId { get; private set; }
    public Guid ProviderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ProviderFavorite Create(Guid userId, Guid providerId, DateTimeOffset now) => new() { UserId = userId, ProviderId = providerId, CreatedAt = now };
}

/// <summary>Réglages de modération (ligne unique).</summary>
public sealed class ServicesSettings
{
    public const int SingletonId = 1;

    private ServicesSettings()
    {
    }

    public int Id { get; private set; }
    public List<string> AlertKeywords { get; private set; } = [];
    public int ReportThreshold { get; private set; }
    public int DailyRequestLimit { get; private set; }

    public static ServicesSettings CreateDefault() => new()
    {
        Id = SingletonId,
        ReportThreshold = 3,
        DailyRequestLimit = 10,
        AlertKeywords =
        [
            "western union", "moneygram", "paiement d'avance", "paiement avant", "mandat cash", "escort", "massage erotique", "massage sensuel",
            "contenu adulte", "arme", "armes", "munitions", "viagra", "medicaments sans ordonnance", "ordonnance", "cannabis", "drogue"
        ]
    };

    public void Update(IEnumerable<string> keywords, int reportThreshold, int dailyRequestLimit)
    {
        if (reportThreshold is < 1 or > 50)
        {
            throw new DomainException("Seuil de signalements : entre 1 et 50.");
        }

        if (dailyRequestLimit is < 1 or > 100)
        {
            throw new DomainException("Demandes par jour et par membre : entre 1 et 100.");
        }

        var list = keywords.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count > 300 || list.Any(k => k.Length > 100))
        {
            throw new DomainException("300 mots-clés au plus, 100 caractères chacun.");
        }

        (AlertKeywords, ReportThreshold, DailyRequestLimit) = (list, reportThreshold, dailyRequestLimit);
    }
}
