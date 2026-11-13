using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Services.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Services.Application;

public enum ServiceSort
{
    Relevance,
    Distance,
    Rating,
    Recent
}

public sealed record SearchServicesQuery(
    Guid? CategoryId, string? Q, double? Lat, double? Lng, double? RadiusKm, string? City, bool? AtHome, double? MinRating, decimal? PriceMax,
    bool? VerifiedOnly, ServiceSort Sort, string? Cursor, int Take = 20) : IQuery<ProviderPageDto>;

public sealed record GetServiceCategoriesQuery : IQuery<IReadOnlyList<CategoryDto>>;

public sealed record GetProviderQuery(Guid Id) : IQuery<ProviderDetailDto>;

public sealed record GetProviderReviewsQuery(Guid ProviderId, string? Cursor, int Take = 10) : IQuery<ReviewPageDto>;

public sealed record GetFavoriteProvidersQuery : IQuery<IReadOnlyList<ProviderCardDto>>;

public sealed record SetProviderFavoriteCommand(Guid ProviderId, bool Favorite) : ICommand<FavoriteDto>;

public sealed record GetServicesInfoQuery : IQuery<ServicesInfoDto>;

/// <summary>Recherche et fiches publiques. Coordonnées exactes jamais renvoyées : distance arrondie et ville seulement.</summary>
public sealed class ServiceSearchHandlers(IServicesDb db, IIdentityModule identity, ServicesOptions options, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<SearchServicesQuery, ProviderPageDto>,
    IRequestHandler<GetServiceCategoriesQuery, IReadOnlyList<CategoryDto>>,
    IRequestHandler<GetProviderQuery, ProviderDetailDto>,
    IRequestHandler<GetProviderReviewsQuery, ReviewPageDto>,
    IRequestHandler<GetFavoriteProvidersQuery, IReadOnlyList<ProviderCardDto>>,
    IRequestHandler<SetProviderFavoriteCommand, FavoriteDto>,
    IRequestHandler<GetServicesInfoQuery, ServicesInfoDto>
{
    private const double DefaultRadiusKm = 25;

    public async Task<ProviderPageDto> Handle(SearchServicesQuery r, CancellationToken ct)
    {
        await ServicesData.EnsureCatalogAsync(db, ct);
        var take = Math.Clamp(r.Take, 1, 50);
        var hasPoint = r.Lat is { } la && r.Lng is { } ln && la is >= -90 and <= 90 && ln is >= -180 and <= 180;
        var radius = Math.Clamp(r.RadiusKm ?? DefaultRadiusKm, 1, 500);

        // Offres listées correspondant aux critères
        var offerings = db.Offerings.AsNoTracking().Where(o => o.IsActive && o.HiddenReason == HiddenReason.None);
        if (r.CategoryId is { } categoryId)
        {
            var ids = await ServicesData.WithChildrenAsync(db, categoryId, ct);
            offerings = offerings.Where(o => ids.Contains(o.CategoryId));
        }

        if (r.PriceMax is { } max)
        {
            offerings = offerings.Where(o => o.PriceType != PriceType.Quote && o.PriceAmount <= max);
        }

        // Prestataires publiés, pré-filtrés en base (rectangle autour du point, ville, critères)
        var providers = db.Providers.AsNoTracking().Where(p => p.Status == ProviderStatus.Active && p.HiddenReason == HiddenReason.None);
        if (hasPoint)
        {
            var box = ServiceRules.BoundingBox(r.Lat!.Value, r.Lng!.Value, radius);
            providers = providers.Where(p => p.Latitude >= box.MinLat && p.Latitude <= box.MaxLat && p.Longitude >= box.MinLng && p.Longitude <= box.MaxLng);
        }
        else if (!string.IsNullOrWhiteSpace(r.City))
        {
            var city = ServicesData.EscapeLike(r.City.Trim()).Trim('%');
            providers = providers.Where(p => EF.Functions.ILike(p.City, city, "\\"));
        }

        if (r.AtHome == true)
        {
            providers = providers.Where(p => p.ServesAtHome);
        }

        if (r.MinRating is { } minRating)
        {
            providers = providers.Where(p => p.RatingCount > 0 && p.RatingAverage >= minRating);
        }

        if (r.VerifiedOnly == true)
        {
            providers = providers.Where(p => p.VerificationLevel == VerificationLevel.Pro);
        }

        if (Text.Like(r.Q) is { } q)
        {
            var pattern = ServicesData.EscapeLike(q);
            var byText = offerings.Where(o => EF.Functions.ILike(o.Title, pattern, "\\") || EF.Functions.ILike(o.Description, pattern, "\\"));
            providers = providers.Where(p => byText.Any(o => o.ProviderId == p.Id)
                                             || (EF.Functions.ILike(p.DisplayName, pattern, "\\") && offerings.Any(o => o.ProviderId == p.Id)));
        }
        else
        {
            providers = providers.Where(p => offerings.Any(o => o.ProviderId == p.Id));
        }

        var blocked = await identity.GetBlockedRelationsAsync(me.Id, ct);
        var candidates = (await providers.OrderByDescending(p => p.RatingCount).ThenBy(p => p.Id).Take(options.SearchCandidates).ToListAsync(ct))
            .Where(p => !blocked.Contains(p.UserId)).ToList();

        // Distance exacte (Haversine) et rayon d'intervention
        var scored = candidates.Select(p => (Provider: p, Distance: hasPoint ? ServiceRules.DistanceKm(r.Lat!.Value, r.Lng!.Value, p.Latitude, p.Longitude) : (double?)null))
            .Where(x => x.Distance is null || (x.Distance <= radius && (r.AtHome != true || x.Distance <= x.Provider.ServiceRadiusKm)))
            .ToList();

        var ordered = r.Sort switch
        {
            ServiceSort.Distance when hasPoint => scored.OrderBy(x => x.Distance).ThenByDescending(x => ServiceRules.Bayesian(x.Provider.RatingAverage, x.Provider.RatingCount)),
            ServiceSort.Rating => scored.OrderByDescending(x => ServiceRules.Bayesian(x.Provider.RatingAverage, x.Provider.RatingCount)).ThenBy(x => x.Distance),
            ServiceSort.Recent => scored.OrderByDescending(x => x.Provider.ApprovedAt),
            _ => scored.OrderByDescending(x => ServiceRules.Score(x.Provider.RatingAverage, x.Provider.RatingCount, x.Distance, radius, x.Provider.ResponseRate))
        };

        var offset = OffsetCursor.Decode(r.Cursor);
        var page = ordered.Skip(offset).Take(take).ToList();
        var cards = await CardsAsync(page.Select(x => (x.Provider, x.Distance)).ToList(), offerings, ct);
        var next = offset + take < scored.Count ? OffsetCursor.Encode(offset + take) : null;
        return new ProviderPageDto(cards, next, scored.Count);
    }

    public async Task<IReadOnlyList<CategoryDto>> Handle(GetServiceCategoriesQuery request, CancellationToken ct)
    {
        await ServicesData.EnsureCatalogAsync(db, ct);
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
        var counts = await ActiveProvidersByCategoryAsync(db, ct);
        return Tree(categories, counts);
    }

    public async Task<ProviderDetailDto> Handle(GetProviderQuery request, CancellationToken ct)
    {
        var p = await db.Providers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct) ?? throw new NotFoundException("Prestataire");
        var mine = p.UserId == me.Id;
        if (!mine && (!p.IsSearchable || (await identity.GetBlockedRelationsAsync(me.Id, ct)).Contains(p.UserId)))
        {
            throw new NotFoundException("Prestataire");
        }

        var offerings = await db.Offerings.AsNoTracking().Where(o => o.ProviderId == p.Id && o.IsActive && o.HiddenReason == HiddenReason.None)
            .OrderBy(o => o.CreatedAt).ToListAsync(ct);
        var ids = offerings.Select(o => o.CategoryId).Distinct().ToList();
        var categories = await db.Categories.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        // Téléphone : public, ou après acceptation d'une demande de ce membre
        var revealed = p.Phone is not null && (p.PhoneIsPublic || mine || await db.Requests.AnyAsync(x =>
            x.ProviderId == p.Id && x.ClientId == me.Id && (x.Status == RequestStatus.Accepted || x.Status == RequestStatus.Completed), ct));
        var favorite = await db.Favorites.AnyAsync(f => f.UserId == me.Id && f.ProviderId == p.Id, ct);
        return new ProviderDetailDto(p.Id, p.UserId, p.DisplayName, p.Bio, p.PhotoPath, p.City, p.Country, p.ServiceRadiusKm, p.ServesAtHome,
            p.HasPremises, p.LegalStatus, p.VerificationLevel, p.RatingAverage, p.RatingCount, p.ResponseRate, p.MedianResponseMinutes,
            p.ContactPreference, revealed ? p.Phone : null, p.ApprovedAt, favorite, mine, categories.Values.Any(c => c.RequiresQualification),
            offerings.Select(o => o.ToDto(categories)).ToList(), await ReviewsAsync(p.Id, 0, 10, ct));
    }

    public async Task<ReviewPageDto> Handle(GetProviderReviewsQuery request, CancellationToken ct) =>
        await ReviewsAsync(request.ProviderId, OffsetCursor.Decode(request.Cursor), Math.Clamp(request.Take, 1, 50), ct);

    public async Task<IReadOnlyList<ProviderCardDto>> Handle(GetFavoriteProvidersQuery request, CancellationToken ct)
    {
        var ids = await db.Favorites.AsNoTracking().Where(f => f.UserId == me.Id).OrderByDescending(f => f.CreatedAt).Select(f => f.ProviderId)
            .Take(200).ToListAsync(ct);
        var providers = (await db.Providers.AsNoTracking().Where(p => ids.Contains(p.Id) && p.Status == ProviderStatus.Active && p.HiddenReason == HiddenReason.None)
            .ToListAsync(ct)).OrderBy(p => ids.IndexOf(p.Id)).ToList();
        var offerings = db.Offerings.AsNoTracking().Where(o => o.IsActive && o.HiddenReason == HiddenReason.None);
        return await CardsAsync(providers.Select(p => (p, (double?)null)).ToList(), offerings, ct);
    }

    public async Task<FavoriteDto> Handle(SetProviderFavoriteCommand request, CancellationToken ct)
    {
        var existing = await db.Favorites.FirstOrDefaultAsync(f => f.UserId == me.Id && f.ProviderId == request.ProviderId, ct);
        if (request.Favorite && existing is null)
        {
            if (!await db.Providers.AnyAsync(p => p.Id == request.ProviderId && p.Status == ProviderStatus.Active, ct))
            {
                throw new NotFoundException("Prestataire");
            }

            db.Favorites.Add(ProviderFavorite.Create(me.Id, request.ProviderId, clock.GetUtcNow()));
        }
        else if (!request.Favorite && existing is not null)
        {
            db.Favorites.Remove(existing);
        }

        await db.SaveChangesAsync(ct);
        return new FavoriteDto(request.ProviderId, request.Favorite);
    }

    public Task<ServicesInfoDto> Handle(GetServicesInfoQuery request, CancellationToken ct) =>
        Task.FromResult(new ServicesInfoDto(ServicesLegal.Intermediation, ServicesLegal.Qualifications, ServicesLegal.Reviews, ServicesLegal.TaxNotice,
            ServicesLegal.TaxLinks.Select(l => new InfoLinkDto(l.Label, l.Url)).ToList()));

    /// <summary>Prestataires actifs par catégorie (une catégorie parente compte ceux de ses sous-catégories).</summary>
    public static async Task<Dictionary<Guid, int>> ActiveProvidersByCategoryAsync(IServicesDb db, CancellationToken ct)
    {
        var pairs = await db.Offerings.AsNoTracking()
            .Where(o => o.IsActive && o.HiddenReason == HiddenReason.None)
            .Join(db.Providers.Where(p => p.Status == ProviderStatus.Active && p.HiddenReason == HiddenReason.None), o => o.ProviderId, p => p.Id,
                (o, p) => new { o.CategoryId, p.Id })
            .Distinct().ToListAsync(ct);
        var parents = await db.Categories.AsNoTracking().Where(c => c.ParentId != null).ToDictionaryAsync(c => c.Id, c => c.ParentId!.Value, ct);
        return pairs.SelectMany(x => parents.TryGetValue(x.CategoryId, out var parent) ? [(x.CategoryId, x.Id), (parent, x.Id)] : new[] { (x.CategoryId, x.Id) })
            .Distinct().GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.Count());
    }

    public static List<CategoryDto> Tree(IReadOnlyList<ServiceCategory> categories, IReadOnlyDictionary<Guid, int> counts)
    {
        CategoryDto Map(ServiceCategory c, IReadOnlyList<CategoryDto> children) =>
            new(c.Id, c.Slug, c.Name, c.Icon, c.ParentId, c.SortOrder, c.IsActive, c.RequiresQualification, counts.GetValueOrDefault(c.Id), children);

        return categories.Where(c => c.ParentId is null || categories.All(p => p.Id != c.ParentId))
            .Select(parent => Map(parent, categories.Where(c => c.ParentId == parent.Id).Select(c => Map(c, [])).ToList()))
            .ToList();
    }

    private async Task<ReviewPageDto> ReviewsAsync(Guid providerId, int offset, int take, CancellationToken ct)
    {
        var query = db.Reviews.AsNoTracking().Where(r => r.ProviderId == providerId && r.Status == ReviewStatus.Published);
        var total = await query.CountAsync(ct);
        var reviews = await query.OrderByDescending(r => r.CreatedAt).Skip(offset).Take(take).ToListAsync(ct);
        var names = await identity.GetNamesAsync(reviews.Select(r => r.AuthorId).Distinct(), ct);
        var now = clock.GetUtcNow();
        return new ReviewPageDto(reviews.Select(r => new ReviewDto(r.Id, r.AuthorId, ShortName(names.GetValueOrDefault(r.AuthorId)), r.Rating, r.Comment,
                r.CreatedAt, r.UpdatedAt, r.ProviderReply, r.RepliedAt, true, r.AuthorId == me.Id && now <= r.CreatedAt.AddDays(ServiceRules.ReviewEditDays)))
            .ToList(), offset + take < total ? OffsetCursor.Encode(offset + take) : null, total);
    }

    /// <summary>« Afi Social » → « Afi S. » (avis publics).</summary>
    public static string ShortName(string? name)
    {
        var parts = (name ?? "Membre").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length < 2 ? parts.FirstOrDefault() ?? "Membre" : $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.";
    }

    private async Task<List<ProviderCardDto>> CardsAsync(IReadOnlyList<(ProviderProfile Provider, double? Distance)> page, IQueryable<ServiceOffering> offerings,
        CancellationToken ct)
    {
        var ids = page.Select(x => x.Provider.Id).ToList();
        var rows = await offerings.Where(o => ids.Contains(o.ProviderId))
            .Select(o => new { o.ProviderId, o.CategoryId, o.PriceType, o.PriceAmount, o.CreatedAt }).ToListAsync(ct);
        var categoryIds = rows.Select(o => o.CategoryId).Distinct().ToList();
        var categories = await db.Categories.AsNoTracking().Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var favorites = (await db.Favorites.AsNoTracking().Where(f => f.UserId == me.Id && ids.Contains(f.ProviderId)).Select(f => f.ProviderId).ToListAsync(ct))
            .ToHashSet();
        return page.Select(x =>
        {
            var own = rows.Where(o => o.ProviderId == x.Provider.Id).ToList();
            var main = own.OrderBy(o => o.CreatedAt).FirstOrDefault();
            var category = main is not null && categories.TryGetValue(main.CategoryId, out var c) ? c : null;
            var priceFrom = own.Where(o => o.PriceType != PriceType.Quote && o.PriceAmount != null).Select(o => o.PriceAmount).Min();
            var p = x.Provider;
            return new ProviderCardDto(p.Id, p.UserId, p.DisplayName, p.PhotoPath, p.City, p.Country, category?.Name ?? "Services",
                category?.Icon ?? "briefcase-outline", p.RatingAverage, p.RatingCount, priceFrom, p.Currency, p.VerificationLevel, p.ServesAtHome,
                p.HasPremises, x.Distance is { } d ? Math.Max(1, Math.Round(d)) : null, p.ResponseRate, favorites.Contains(p.Id)); // arrondi au km : pas de triangulation fine
        }).ToList();
    }
}
