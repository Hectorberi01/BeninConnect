namespace BeninConnect.Modules.Voyages.Domain;

public enum TripStatus
{
    Open,
    Full,
    Departed,
    Cancelled
}

public enum TripHiddenReason
{
    None,
    Reports,
    Admin,

    /// <summary>Voyageur suspendu par la modération.</summary>
    Suspended
}

public enum ParcelCategory
{
    Documents,
    Clothes,
    DryFood,
    Electronics,
    Cosmetics,
    Other
}

public enum BookingStatus
{
    Pending,
    Accepted,
    Declined,
    Cancelled,
    Delivered,
    Disputed
}

public enum VoyageReportTarget
{
    Trip,
    Booking,
    Review
}

/// <summary>Décision de la modération sur un litige.</summary>
public enum DisputeOutcome
{
    Delivered,
    Cancelled
}
