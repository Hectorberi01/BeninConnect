using BeninConnect.Modules.Services.Domain;

namespace BeninConnect.Modules.Services.Application;

public sealed record CategoryDto(
    Guid Id, string Slug, string Name, string Icon, Guid? ParentId, int SortOrder, bool IsActive, bool RequiresQualification, int ActiveProviders,
    IReadOnlyList<CategoryDto> Children);

public sealed record OfferingDto(
    Guid Id, Guid CategoryId, string CategoryName, bool RequiresQualification, string Title, string Description, PriceType PriceType,
    decimal? PriceAmount, string Currency, int? DurationMinutes, IReadOnlyList<string> Images, bool IsActive, HiddenReason HiddenReason,
    string? AlertKeyword);

public sealed record ReviewDto(
    Guid Id, Guid AuthorId, string AuthorName, int Rating, string? Comment, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
    string? ProviderReply, DateTimeOffset? RepliedAt, bool Verified, bool CanEdit);

public sealed record ReviewPageDto(IReadOnlyList<ReviewDto> Items, string? NextCursor, int Total);

public sealed record DocumentDto(Guid Id, DocumentKind Kind, string OriginalName, string ContentType, long Size, DocumentStatus Status, string? ReviewNote,
    DateTimeOffset UploadedAt, DateTimeOffset? ReviewedAt);

/// <summary>Carte de résultat : jamais de coordonnées, distance arrondie.</summary>
public sealed record ProviderCardDto(
    Guid Id, Guid UserId, string DisplayName, string? PhotoPath, string City, string Country, string CategoryName, string CategoryIcon,
    double RatingAverage, int RatingCount, decimal? PriceFrom, string Currency, VerificationLevel VerificationLevel, bool ServesAtHome,
    bool HasPremises, double? DistanceKm, double ResponseRate, bool IsFavorite);

public sealed record ProviderPageDto(IReadOnlyList<ProviderCardDto> Items, string? NextCursor, int Total);

public sealed record ProviderDetailDto(
    Guid Id, Guid UserId, string DisplayName, string? Bio, string? PhotoPath, string City, string Country, int ServiceRadiusKm, bool ServesAtHome,
    bool HasPremises, LegalStatus LegalStatus, VerificationLevel VerificationLevel, double RatingAverage, int RatingCount, double ResponseRate,
    int? MedianResponseMinutes, ContactPreference ContactPreference, string? Phone, DateTimeOffset? MemberSince, bool IsFavorite, bool IsMine,
    bool HasRegulatedServices, IReadOnlyList<OfferingDto> Offerings, ReviewPageDto Reviews);

/// <summary>Profil vu par son titulaire (toutes les données, offres masquées comprises).</summary>
public sealed record MyProviderDto(
    Guid Id, ProviderStatus Status, string? ReviewNote, string DisplayName, string? Bio, string? PhotoPath, string? Phone, bool PhoneIsPublic,
    ContactPreference ContactPreference, LegalStatus LegalStatus, string? Siret, string? Ifu, string Country, string City, string? PostalCode,
    double Latitude, double Longitude, int ServiceRadiusKm, bool ServesAtHome, bool HasPremises, bool QualificationDeclared,
    VerificationLevel VerificationLevel, HiddenReason HiddenReason, double RatingAverage, int RatingCount, double ResponseRate,
    int? MedianResponseMinutes, string Currency, IReadOnlyList<OfferingDto> Offerings, IReadOnlyList<DocumentDto> Documents);

public sealed record MeDto(bool HasProfile, MyProviderDto? Profile);

public sealed record RequestDto(
    Guid Id, RequestStatus Status, Guid OfferingId, string OfferingTitle, Guid ProviderId, string ProviderName, string? ProviderPhoto,
    Guid ClientId, string ClientName, string Message, DateOnly? PreferredDate, string? City, Guid? ConversationId, string? DeclineReason,
    DateTimeOffset CreatedAt, DateTimeOffset? ViewedAt, DateTimeOffset? RespondedAt, DateTimeOffset? CompletedAt, string? ProviderPhone,
    bool IsProvider, bool CanReview, Guid? ReviewId);

public sealed record InfoLinkDto(string Label, string Url);

public sealed record ServicesInfoDto(string Intermediation, string Qualifications, string Reviews, string TaxNotice, IReadOnlyList<InfoLinkDto> TaxLinks);

public sealed record UploadedImageDto(string Path);

public sealed record FavoriteDto(Guid ProviderId, bool IsFavorite);

public static class ServicesMapping
{
    public static OfferingDto ToDto(this ServiceOffering o, IReadOnlyDictionary<Guid, ServiceCategory> categories) =>
        new(o.Id, o.CategoryId, categories.TryGetValue(o.CategoryId, out var c) ? c.Name : "Service", c?.RequiresQualification ?? false,
            o.Title, o.Description, o.PriceType, o.PriceAmount, o.Currency, o.DurationMinutes, o.Images, o.IsActive, o.HiddenReason, o.AlertKeyword);

    public static DocumentDto ToDto(this ProviderDocument d) =>
        new(d.Id, d.Kind, d.OriginalName, d.ContentType, d.Size, d.Status, d.ReviewNote, d.UploadedAt, d.ReviewedAt);

    public static MyProviderDto ToMine(this ProviderProfile p, IReadOnlyList<OfferingDto> offerings, IReadOnlyList<DocumentDto> documents) =>
        new(p.Id, p.Status, p.ReviewNote, p.DisplayName, p.Bio, p.PhotoPath, p.Phone, p.PhoneIsPublic, p.ContactPreference, p.LegalStatus, p.Siret, p.Ifu,
            p.Country, p.City, p.PostalCode, p.Latitude, p.Longitude, p.ServiceRadiusKm, p.ServesAtHome, p.HasPremises, p.QualificationDeclared,
            p.VerificationLevel, p.HiddenReason, p.RatingAverage, p.RatingCount, p.ResponseRate, p.MedianResponseMinutes, p.Currency, offerings,
            documents);
}
