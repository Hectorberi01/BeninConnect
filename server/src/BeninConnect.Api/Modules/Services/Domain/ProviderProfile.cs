using System.Text.RegularExpressions;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Services.Domain;

/// <summary>Données du profil saisies par le prestataire.</summary>
public sealed record ProviderDetails(
    string DisplayName, string? Bio, string? PhotoPath, string? Phone, bool PhoneIsPublic, ContactPreference ContactPreference,
    LegalStatus LegalStatus, string? Siret, string? Ifu, string Country, string City, string? PostalCode, double Latitude, double Longitude,
    int ServiceRadiusKm, bool ServesAtHome, bool HasPremises);

/// <summary>Profil prestataire (un par membre). Première publication validée par l'équipe.</summary>
public sealed partial class ProviderProfile
{
    private ProviderProfile()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? Bio { get; private set; }
    public string? PhotoPath { get; private set; }
    public string? Phone { get; private set; }

    /// <summary>Faux : numéro donné seulement après acceptation d'une demande.</summary>
    public bool PhoneIsPublic { get; private set; }

    public ContactPreference ContactPreference { get; private set; }
    public LegalStatus LegalStatus { get; private set; }
    public string? Siret { get; private set; }
    public string? Ifu { get; private set; }
    public string Country { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string? PostalCode { get; private set; }

    /// <summary>Coordonnées précises : recherche seulement, JAMAIS renvoyées aux membres.</summary>
    public double Latitude { get; private set; }

    public double Longitude { get; private set; }
    public int ServiceRadiusKm { get; private set; }
    public bool ServesAtHome { get; private set; }
    public bool HasPremises { get; private set; }

    /// <summary>Déclaration sur l'honneur (métiers réglementés).</summary>
    public bool QualificationDeclared { get; private set; }

    public ProviderStatus Status { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public VerificationLevel VerificationLevel { get; private set; }

    /// <summary>Masqué des recherches en attendant une revue (signalements, mot-clé d'alerte).</summary>
    public HiddenReason HiddenReason { get; private set; }

    public string? AlertKeyword { get; private set; }
    public int ReportCount { get; private set; }

    public double RatingAverage { get; private set; }
    public int RatingCount { get; private set; }
    public double ResponseRate { get; private set; }
    public int? MedianResponseMinutes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsSearchable => Status == ProviderStatus.Active && HiddenReason == HiddenReason.None;

    public bool NeedsReview => Status == ProviderStatus.PendingReview || HiddenReason is HiddenReason.AlertKeyword or HiddenReason.Reports;

    public string Currency => ServiceRules.CurrencyFor(Country);

    public static ProviderProfile Create(Guid userId, ProviderDetails details, bool qualificationDeclared, DateTimeOffset now)
    {
        var profile = new ProviderProfile { Id = Guid.CreateVersion7(now), UserId = userId, Status = ProviderStatus.Draft, CreatedAt = now };
        profile.Update(details, qualificationDeclared, now);
        return profile;
    }

    public void Update(ProviderDetails d, bool qualificationDeclared, DateTimeOffset now)
    {
        if (Status == ProviderStatus.Suspended)
        {
            throw new DomainException("Profil suspendu : contactez l'équipe BéninConnect.");
        }

        var name = (d.DisplayName ?? string.Empty).Trim();
        if (name.Length is < 2 or > 80)
        {
            throw new DomainException("Nom affiché : 2 à 80 caractères.");
        }

        var country = (d.Country ?? string.Empty).Trim().ToUpperInvariant();
        if (country.Length != 2)
        {
            throw new DomainException("Pays : code à 2 lettres (FR, BJ…).");
        }

        if (string.IsNullOrWhiteSpace(d.City))
        {
            throw new DomainException("Indiquez votre ville.");
        }

        if (d.Latitude is < -90 or > 90 || d.Longitude is < -180 or > 180)
        {
            throw new DomainException("Position invalide.");
        }

        if (d.ServiceRadiusKm is < 1 or > 200)
        {
            throw new DomainException("Rayon d'intervention : 1 à 200 km.");
        }

        if (!d.ServesAtHome && !d.HasPremises)
        {
            throw new DomainException("Indiquez si vous vous déplacez, si vous recevez, ou les deux.");
        }

        var phone = ServiceRules.CleanOrNull(d.Phone, 20);
        if (phone is not null && !PhonePattern().IsMatch(phone))
        {
            throw new DomainException("Numéro de téléphone invalide (format international conseillé : +33…, +229…).");
        }

        if (d.ContactPreference != ContactPreference.InApp && phone is null)
        {
            throw new DomainException("Indiquez un numéro pour être contacté par téléphone ou WhatsApp.");
        }

        var siret = ServiceRules.CleanOrNull(d.Siret?.Replace(" ", ""), 14);
        if (siret is not null && !SiretPattern().IsMatch(siret))
        {
            throw new DomainException("SIRET : 14 chiffres.");
        }

        DisplayName = name;
        Bio = ServiceRules.CleanOrNull(d.Bio, 1500);
        PhotoPath = ServiceRules.CleanOrNull(d.PhotoPath, 300);
        Phone = phone;
        PhoneIsPublic = phone is not null && d.PhoneIsPublic;
        ContactPreference = d.ContactPreference;
        LegalStatus = d.LegalStatus;
        Siret = siret;
        Ifu = ServiceRules.CleanOrNull(d.Ifu?.Replace(" ", ""), 20);
        Country = country;
        City = ServiceRules.Clean(d.City, 80);
        PostalCode = ServiceRules.CleanOrNull(d.PostalCode, 10);
        Latitude = d.Latitude;
        Longitude = d.Longitude;
        ServiceRadiusKm = d.ServiceRadiusKm;
        ServesAtHome = d.ServesAtHome;
        HasPremises = d.HasPremises;
        QualificationDeclared = qualificationDeclared;
        UpdatedAt = now;

        // Numéro d'entreprise retiré : plus de badge « Pro vérifié »
        if (VerificationLevel == VerificationLevel.Pro && !HasBusinessNumber)
        {
            VerificationLevel = VerificationLevel.Identity;
        }
    }

    public bool HasBusinessNumber => Siret is not null || Ifu is not null;

    /// <summary>Envoi en revue (première publication, ou après un refus).</summary>
    public void Submit(int activeOfferings, bool needsDeclaration, DateTimeOffset now)
    {
        if (Status is not ProviderStatus.Draft)
        {
            throw new DomainException(Status == ProviderStatus.PendingReview ? "Votre profil est déjà en cours de vérification." : "Ce profil est déjà publié.");
        }

        if (activeOfferings == 0)
        {
            throw new DomainException("Ajoutez au moins une offre avant l'envoi en vérification.");
        }

        if (needsDeclaration && !QualificationDeclared)
        {
            throw new DomainException("Cochez la déclaration sur l'honneur : un de vos services est un métier réglementé.");
        }

        Status = ProviderStatus.PendingReview;
        SubmittedAt = now;
        ReviewNote = null;
    }

    public void Approve(DateTimeOffset now)
    {
        if (Status != ProviderStatus.PendingReview)
        {
            throw new DomainException("Ce profil n'est pas en attente de validation.");
        }

        Status = ProviderStatus.Active;
        ApprovedAt ??= now;
        ReviewNote = null;
    }

    /// <summary>Refus : retour en brouillon avec le motif, le prestataire corrige puis renvoie.</summary>
    public void Reject(string reason)
    {
        if (Status != ProviderStatus.PendingReview)
        {
            throw new DomainException("Ce profil n'est pas en attente de validation.");
        }

        Status = ProviderStatus.Draft;
        ReviewNote = RequireReason(reason);
    }

    public void Suspend(string reason)
    {
        Status = ProviderStatus.Suspended;
        ReviewNote = RequireReason(reason);
    }

    /// <summary>Levée de suspension ou de masquage : de nouveau visible.</summary>
    public void Reinstate()
    {
        if (Status == ProviderStatus.Suspended)
        {
            Status = ApprovedAt is null ? ProviderStatus.Draft : ProviderStatus.Active;
        }

        HiddenReason = HiddenReason.None;
        AlertKeyword = null;
        ReportCount = 0;
        ReviewNote = null;
    }

    public void FlagAlert(string keyword)
    {
        AlertKeyword = ServiceRules.Clean(keyword, 100);
        HiddenReason = HiddenReason.AlertKeyword;
    }

    /// <summary>Retourne vrai si ce signalement masque le profil.</summary>
    public bool AddReport(int threshold)
    {
        ReportCount++;
        if (HiddenReason != HiddenReason.None || ReportCount < Math.Max(1, threshold))
        {
            return false;
        }

        HiddenReason = HiddenReason.Reports;
        return true;
    }

    /// <summary>Niveau accordé après validation d'un justificatif. Pro : numéro SIRET ou IFU obligatoire.</summary>
    public void Verify(VerificationLevel level, DateTimeOffset now)
    {
        if (level == VerificationLevel.Pro && !HasBusinessNumber)
        {
            throw new DomainException("Badge « Pro vérifié » : le prestataire doit d'abord renseigner son SIRET ou son IFU.");
        }

        if (level > VerificationLevel)
        {
            VerificationLevel = level;
            VerifiedAt = now;
        }
    }

    public void RevokeVerification() => (VerificationLevel, VerifiedAt) = (VerificationLevel.None, null);

    public void SetRating(double average, int count) => (RatingAverage, RatingCount) = (Math.Round(average, 2), count);

    public void SetResponseStats(double rate, int? medianMinutes) => (ResponseRate, MedianResponseMinutes) = (Math.Round(rate, 3), medianMinutes);

    private static string RequireReason(string? reason)
    {
        var text = (reason ?? string.Empty).Trim();
        return text.Length is >= 3 and <= 500 ? text : throw new DomainException("Indiquez le motif (3 à 500 caractères).");
    }

    [GeneratedRegex(@"^\+?[0-9 .\-]{8,20}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^\d{14}$")]
    private static partial Regex SiretPattern();
}

/// <summary>Justificatif privé (pièce d'identité, diplôme, inscription au registre), vu seulement par l'équipe.</summary>
public sealed class ProviderDocument
{
    private ProviderDocument()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProviderId { get; private set; }
    public DocumentKind Kind { get; private set; }

    /// <summary>Pièce jointe privée du module Media (jamais d'URL publique).</summary>
    public string FileName { get; private set; } = string.Empty;

    public string OriginalName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public DocumentStatus Status { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public static ProviderDocument Create(Guid providerId, DocumentKind kind, string fileName, string originalName, string contentType, long size,
        DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        ProviderId = providerId,
        Kind = kind,
        FileName = fileName,
        OriginalName = ServiceRules.Clean(string.IsNullOrWhiteSpace(originalName) ? "justificatif" : Path.GetFileName(originalName), 150),
        ContentType = contentType,
        Size = size,
        Status = DocumentStatus.Pending,
        UploadedAt = now
    };

    public void Approve(DateTimeOffset now) => Decide(DocumentStatus.Approved, null, now);

    public void Reject(string reason, DateTimeOffset now)
    {
        var text = (reason ?? string.Empty).Trim();
        Decide(DocumentStatus.Rejected, text.Length is >= 3 and <= 500 ? text : throw new DomainException("Indiquez le motif du refus."), now);
    }

    private void Decide(DocumentStatus status, string? note, DateTimeOffset now)
    {
        if (Status != DocumentStatus.Pending)
        {
            throw new DomainException("Ce justificatif a déjà été traité.");
        }

        (Status, ReviewNote, ReviewedAt) = (status, note, now);
    }
}
