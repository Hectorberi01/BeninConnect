using BeninConnect.Modules.Voyages.Domain;

namespace BeninConnect.Modules.Voyages.Application;

/// <summary>Membre affiché : prénom et initiale, jamais de téléphone ni d'email (contact par la messagerie).</summary>
public sealed record PersonDto(Guid Id, string Name, string AvatarColor, string? AvatarImage);

public sealed record TripCardDto(
    Guid Id, PersonDto Traveler, string FromCountry, string FromCity, string ToCountry, string ToCity, DateOnly DepartureDate,
    decimal CapacityKg, decimal RemainingKg, decimal? PricePerKg, string Currency, IReadOnlyList<ParcelCategory> AcceptedCategories,
    TripStatus Status, double TravelerRating, int TravelerReviewCount, bool TrustedTraveler, bool IsMine);

public sealed record TripDetailDto(
    TripCardDto Trip, string? DropOffZones, string? Notes, int TravelerDeliveries, BookingDto? MyBooking, bool CanBook,
    TripHiddenReason HiddenReason, DateTimeOffset CreatedAt);

public sealed record TripPageDto(IReadOnlyList<TripCardDto> Items, string? NextCursor, int Total);

public sealed record BookingDto(
    Guid Id, Guid TripId, string FromCity, string ToCity, DateOnly DepartureDate, TripStatus TripStatus, PersonDto Sender, PersonDto Traveler,
    bool IAmTraveler, decimal WeightKg, ParcelCategory Category, string Description, BookingStatus Status, string? DeclineReason,
    Guid? ConversationId, bool HasCode, bool CodeLocked, int AttemptsLeft, DateTimeOffset CreatedAt, DateTimeOffset? RespondedAt,
    DateTimeOffset? DeliveredAt, string? DisputeReason, string? ResolutionNote, bool CanReview, Guid? MyReviewId);

/// <summary>Trajet du voyageur avec les réservations reçues.</summary>
public sealed record MyTripDto(TripDetailDto Detail, IReadOnlyList<BookingDto> Bookings, int PendingCount);

/// <summary>Code de remise en clair : montré une seule fois à l'expéditeur.</summary>
public sealed record DeliveryCodeDto(string Code, string ShareText);

public sealed record VoyageReviewDto(Guid Id, PersonDto Author, int Rating, string? Comment, DateTimeOffset CreatedAt, bool AboutTraveler);

public sealed record VoyageReviewPageDto(IReadOnlyList<VoyageReviewDto> Items, double RatingAverage, int RatingCount);

/// <summary>Bloc « Voyages » du profil.</summary>
public sealed record VoyageStatsDto(
    int DeliveriesAsTraveler, int ShipmentsDelivered, double RatingAverage, int RatingCount, bool TrustedTraveler, IReadOnlyList<TripCardDto> UpcomingTrips);

public sealed record RouteAlertDto(
    Guid Id, string FromCountry, string? FromCity, string ToCountry, string? ToCity, DateOnly? After, DateOnly? Before, DateTimeOffset CreatedAt);

public sealed record LinkDto(string Label, string Url);

public sealed record VoyagesInfoDto(
    string Intermediation, string TravelerResponsibility, IReadOnlyList<string> ProhibitedItems, IReadOnlyList<string> TravelerAdvice,
    IReadOnlyList<LinkDto> CustomsLinks, int MaxDaysAhead, decimal MinBookingKg, decimal MaxBookingKg, decimal MaxCapacityKg, int MaxAlerts);
