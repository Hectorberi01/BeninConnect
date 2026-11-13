namespace BeninConnect.Modules.Services.Domain;

public enum ProviderStatus
{
    Draft,
    PendingReview,
    Active,
    Suspended
}

public enum VerificationLevel
{
    None,
    Identity,
    Pro
}

public enum LegalStatus
{
    Individual,
    MicroEntrepreneur,
    Company
}

public enum ContactPreference
{
    InApp,
    Phone,
    WhatsApp
}

public enum PriceType
{
    Fixed,
    From,
    Hourly,
    Quote
}

public enum RequestStatus
{
    Sent,
    Viewed,
    Accepted,
    Declined,
    Completed,
    Cancelled
}

public enum ReviewStatus
{
    Published,
    Hidden
}

/// <summary>Masquage : automatique (en attente de revue) ou décidé par l'équipe.</summary>
public enum HiddenReason
{
    None,
    AlertKeyword,
    Reports,
    Admin
}

public enum DocumentKind
{
    Identity,
    Qualification,
    Registration
}

public enum DocumentStatus
{
    Pending,
    Approved,
    Rejected
}

public enum ReportTarget
{
    Profile,
    Offering,
    Review
}
