using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Jobs.Domain;

/// <summary>Offre importée d'une API externe. La candidature se fait chez la source (ApplyUrl).</summary>
public sealed class JobOffer
{
    private JobOffer()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Ordre d'insertion : départage la pagination par curseur à date égale.</summary>
    public long Number { get; private set; }

    public JobSource Source { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? CompanyName { get; private set; }
    public string? CompanyLogoUrl { get; private set; }
    public string Country { get; private set; } = string.Empty;
    public string LocationLabel { get; private set; } = string.Empty;
    public string? PostalCode { get; private set; }
    public string? Department { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public ContractType ContractType { get; private set; }
    public string? ContractLabel { get; private set; }
    public bool IsRemote { get; private set; }
    public string? WorkingTime { get; private set; }
    public string? SalaryLabel { get; private set; }
    public string? SectorLabel { get; private set; }
    public string ApplyUrl { get; private set; } = string.Empty;
    public string SourceLabel { get; private set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset? ClosingAt { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset? ExpiredAt { get; private set; }
    public JobOfferStatus Status { get; private set; }
    public HiddenReason HiddenReason { get; private set; }

    /// <summary>Masquée automatiquement : visible dans la file de la console jusqu'à décision.</summary>
    public bool NeedsReview { get; private set; }

    public string? AlertKeyword { get; private set; }
    public int ReportCount { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public string SearchKey { get; private set; } = string.Empty;

    /// <summary>Même offre déjà affichée depuis une source prioritaire : exclue des listes.</summary>
    public Guid? DuplicateOfId { get; private set; }

    public bool IsVisible => Status == JobOfferStatus.Active && DuplicateOfId is null;

    public static JobOffer Import(JobSource source, ImportedOfferData data, string searchKey, DateTimeOffset now)
    {
        var offer = new JobOffer
        {
            Id = Guid.CreateVersion7(now),
            Source = source,
            ExternalId = JobText.Cut(data.ExternalId, 200),
            ImportedAt = now,
            Status = JobOfferStatus.Active
        };
        offer.Apply(data, searchKey, now);
        if (offer.ClosingAt is { } closing && closing <= now)
        {
            offer.Expire(now);
        }

        return offer;
    }

    /// <summary>Revue à l'import : contenu mis à jour. Expirée et toujours publiée : réactivée. Masquée : reste masquée.</summary>
    public void Refresh(ImportedOfferData data, string searchKey, DateTimeOffset now)
    {
        Apply(data, searchKey, now);
        if (Status == JobOfferStatus.Expired && (ClosingAt is null || ClosingAt > now))
        {
            Status = JobOfferStatus.Active;
            ExpiredAt = null;
        }
    }

    public void Touch(DateTimeOffset now) => LastSeenAt = now;

    public bool Expire(DateTimeOffset now)
    {
        if (Status != JobOfferStatus.Active)
        {
            return false;
        }

        Status = JobOfferStatus.Expired;
        ExpiredAt = now;
        return true;
    }

    public void FlagAlert(string keyword)
    {
        AlertKeyword = JobText.Cut(keyword, 100);
        Hide(HiddenReason.AlertKeyword);
    }

    /// <summary>Masquage : automatique (en attente de revue) ou décidé par un administrateur (définitif).</summary>
    public void Hide(HiddenReason reason)
    {
        if (reason == HiddenReason.None)
        {
            throw new DomainException("Motif de masquage requis.");
        }

        Status = JobOfferStatus.Hidden;
        HiddenReason = reason;
        NeedsReview = reason != HiddenReason.Admin;
    }

    /// <summary>Réaffichage par un administrateur : signalements remis à zéro.</summary>
    public void Show(DateTimeOffset now)
    {
        if (Status != JobOfferStatus.Hidden)
        {
            throw new DomainException("Cette offre n'est pas masquée.");
        }

        Status = ClosingAt is { } closing && closing <= now ? JobOfferStatus.Expired : JobOfferStatus.Active;
        ExpiredAt = Status == JobOfferStatus.Expired ? now : null;
        HiddenReason = HiddenReason.None;
        NeedsReview = false;
        ReportCount = 0;
    }

    /// <summary>Retourne vrai si ce signalement masque l'offre.</summary>
    public bool AddReport(int threshold)
    {
        ReportCount++;
        if (Status == JobOfferStatus.Hidden || ReportCount < Math.Max(1, threshold))
        {
            return false;
        }

        Hide(HiddenReason.Reports);
        return true;
    }

    public void MarkDuplicateOf(Guid? original) => DuplicateOfId = original == Id ? null : original;

    private void Apply(ImportedOfferData d, string searchKey, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(d.Title) || string.IsNullOrWhiteSpace(d.ApplyUrl))
        {
            throw new DomainException("Offre sans titre ou sans lien de candidature.");
        }

        Title = JobText.Cut(d.Title, 300);
        Description = JobText.Cut(d.Description, 10_000);
        CompanyName = JobText.CutOrNull(d.CompanyName, 200);
        CompanyLogoUrl = d.CompanyLogoUrl is { } logo && logo.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? JobText.Cut(logo, 1000) : null;
        Country = JobText.Cut(d.Country, 2).ToUpperInvariant();
        LocationLabel = JobText.Cut(d.LocationLabel, 200);
        PostalCode = JobText.CutOrNull(d.PostalCode, 10);
        Department = JobText.CutOrNull(d.Department, 3);
        Latitude = d.Latitude;
        Longitude = d.Longitude;
        ContractType = d.ContractType;
        ContractLabel = JobText.CutOrNull(d.ContractLabel, 100);
        IsRemote = d.IsRemote;
        WorkingTime = JobText.CutOrNull(d.WorkingTime, 100);
        SalaryLabel = JobText.CutOrNull(d.SalaryLabel, 200);
        SectorLabel = JobText.CutOrNull(d.SectorLabel, 200);
        ApplyUrl = JobText.Cut(d.ApplyUrl, 2000);
        SourceLabel = JobText.Cut(d.SourceLabel ?? JobSources.Label(Source), 100);
        PublishedAt = d.PublishedAt > now ? now : d.PublishedAt;
        ClosingAt = d.ClosingAt;
        SearchKey = JobText.Cut(searchKey, 50);
        LastSeenAt = now;
        Fingerprint = JobText.Fingerprint(Title, CompanyName, LocationLabel);
    }
}

/// <summary>Signalement d'un membre (un par membre et par offre).</summary>
public sealed class JobOfferReport
{
    private JobOfferReport()
    {
    }

    public Guid Id { get; private set; }
    public Guid OfferId { get; private set; }
    public Guid UserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool Handled { get; private set; }

    public static JobOfferReport Create(Guid offerId, Guid userId, string reason, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        OfferId = offerId,
        UserId = userId,
        Reason = JobText.Cut(reason, 500),
        CreatedAt = now
    };

    public void MarkHandled() => Handled = true;
}

public sealed class JobOfferFavorite
{
    private JobOfferFavorite()
    {
    }

    public Guid UserId { get; private set; }
    public Guid OfferId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static JobOfferFavorite Create(Guid userId, Guid offerId, DateTimeOffset now) => new() { UserId = userId, OfferId = offerId, CreatedAt = now };
}
