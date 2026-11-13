using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Modules.Services.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Services.Application;

public sealed record ServicesSettingsDto(IReadOnlyList<string> AlertKeywords, int ReportThreshold, int DailyRequestLimit);

public sealed record ServicesOverviewDto(
    int PendingProfiles, int OfferingsToReview, int ReviewsToReview, int OpenReports, int PendingDocuments, int HiddenProfiles, int ActiveProviders,
    int TotalProviders, int RequestsLast7Days, int CompletedLast30Days, int Reviews, double AverageRating, ServicesSettingsDto Settings);

public sealed record CategoryCountDto(Guid Id, string Name, string Icon, string? ParentName, int Providers);

public sealed record CityCountDto(string City, string Country, int Providers);

public sealed record WeekCountDto(DateOnly WeekStart, int Requests, int Accepted, int Completed);

public sealed record ServicesStatsDto(IReadOnlyList<CategoryCountDto> ByCategory, IReadOnlyList<CityCountDto> ByCity, IReadOnlyList<WeekCountDto> RequestsByWeek);

public sealed record AdminProviderDto(
    Guid Id, Guid UserId, string MemberName, string DisplayName, string? PhotoPath, string City, string Country, ProviderStatus Status,
    HiddenReason HiddenReason, string? AlertKeyword, int ReportCount, VerificationLevel VerificationLevel, LegalStatus LegalStatus, int Offerings,
    double RatingAverage, int RatingCount, DateTimeOffset? SubmittedAt, DateTimeOffset? ApprovedAt, DateTimeOffset CreatedAt, int PendingDocuments);

public sealed record AdminReportDto(
    Guid Id, ReportTarget TargetType, Guid TargetId, string TargetLabel, Guid? ProviderId, string AuthorName, string Reason, DateTimeOffset CreatedAt,
    bool Handled, string? Decision);

public sealed record AdminProviderDetailDto(
    AdminProviderDto Provider, string? Bio, string? Phone, bool PhoneIsPublic, ContactPreference ContactPreference, string? Siret, string? Ifu,
    string? PostalCode, double Latitude, double Longitude, int ServiceRadiusKm, bool ServesAtHome, bool HasPremises, bool QualificationDeclared,
    bool HasRegulatedServices, string? ReviewNote, double ResponseRate, int RequestsReceived, int RequestsCompleted, IReadOnlyList<OfferingDto> Offerings,
    IReadOnlyList<DocumentDto> Documents, IReadOnlyList<AdminReportDto> Reports);

public sealed record AdminOfferingDto(
    OfferingDto Offering, Guid ProviderId, string ProviderName, ProviderStatus ProviderStatus, string? ModerationNote, int ReportCount,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record AdminReviewDto(
    Guid Id, Guid ProviderId, string ProviderName, string AuthorName, int Rating, string? Comment, DateTimeOffset CreatedAt, ReviewStatus Status,
    HiddenReason HiddenReason, string? ModerationNote, int ReportCount, string? ProviderReply);

public sealed record AdminDocumentDto(
    DocumentDto Document, Guid ProviderId, string ProviderName, LegalStatus LegalStatus, string? Siret, string? Ifu, VerificationLevel VerificationLevel);

public sealed record DocumentFile(Stream Content, long? Length, string ContentType, string FileName);

public enum ProviderView
{
    Review,
    Pending,
    Active,
    Hidden,
    Suspended,
    Draft,
    All
}

public enum ModerationView
{
    Review,
    Hidden,
    All
}

public sealed record GetServicesOverviewQuery : IQuery<ServicesOverviewDto>;

public sealed record GetServicesStatsQuery : IQuery<ServicesStatsDto>;

public sealed record GetAdminProvidersQuery(ProviderView View, string? Q, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminProviderDto>>;

public sealed record GetAdminProviderQuery(Guid Id) : IQuery<AdminProviderDetailDto>;

public sealed record ApproveProviderCommand(Guid Id) : ICommand<AdminProviderDetailDto>;

public sealed record RejectProviderCommand(Guid Id, string Reason) : ICommand<AdminProviderDetailDto>;

public sealed record SuspendProviderCommand(Guid Id, string Reason) : ICommand<AdminProviderDetailDto>;

public sealed record ReinstateProviderCommand(Guid Id) : ICommand<AdminProviderDetailDto>;

public sealed record RevokeVerificationCommand(Guid Id) : ICommand<AdminProviderDetailDto>;

public sealed record GetAdminOfferingsQuery(ModerationView View, string? Q, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminOfferingDto>>;

public sealed record SetOfferingVisibilityCommand(Guid Id, bool Visible, string? Reason) : ICommand<AdminOfferingDto>;

public sealed record GetAdminReviewsQuery(ModerationView View, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminReviewDto>>;

public sealed record SetReviewVisibilityCommand(Guid Id, bool Visible, string? Reason) : ICommand<AdminReviewDto>;

public sealed record GetAdminServiceReportsQuery(bool Handled, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminReportDto>>;

public sealed record CloseServiceReportCommand(Guid Id, string? Decision) : ICommand<AdminReportDto>;

public sealed record GetAdminDocumentsQuery(DocumentStatus Status, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminDocumentDto>>;

public sealed record GetDocumentFileQuery(Guid Id) : IQuery<DocumentFile>;

public sealed record DecideDocumentCommand(Guid Id, bool Approve, string? Reason) : ICommand<AdminDocumentDto>;

public sealed record GetAdminCategoriesQuery : IQuery<IReadOnlyList<CategoryDto>>;

public sealed record SaveCategoryCommand(Guid? Id, string Slug, string Name, string Icon, Guid? ParentId, int SortOrder, bool IsActive,
    bool RequiresQualification) : ICommand<CategoryDto>;

public sealed record UpdateServicesSettingsCommand(IReadOnlyList<string> AlertKeywords, int ReportThreshold, int DailyRequestLimit)
    : ICommand<ServicesSettingsDto>;

/// <summary>Console : modération (profils, offres, avis, signalements, justificatifs), catégories, réglages et tableau de bord.</summary>
public sealed class AdminServiceHandlers(IServicesDb db, IIdentityModule identity, IMediaModule media, IEventBus bus, IAdminSession admin, TimeProvider clock) :
    IRequestHandler<GetServicesOverviewQuery, ServicesOverviewDto>,
    IRequestHandler<GetServicesStatsQuery, ServicesStatsDto>,
    IRequestHandler<GetAdminProvidersQuery, PagedResult<AdminProviderDto>>,
    IRequestHandler<GetAdminProviderQuery, AdminProviderDetailDto>,
    IRequestHandler<ApproveProviderCommand, AdminProviderDetailDto>,
    IRequestHandler<RejectProviderCommand, AdminProviderDetailDto>,
    IRequestHandler<SuspendProviderCommand, AdminProviderDetailDto>,
    IRequestHandler<ReinstateProviderCommand, AdminProviderDetailDto>,
    IRequestHandler<RevokeVerificationCommand, AdminProviderDetailDto>,
    IRequestHandler<GetAdminOfferingsQuery, PagedResult<AdminOfferingDto>>,
    IRequestHandler<SetOfferingVisibilityCommand, AdminOfferingDto>,
    IRequestHandler<GetAdminReviewsQuery, PagedResult<AdminReviewDto>>,
    IRequestHandler<SetReviewVisibilityCommand, AdminReviewDto>,
    IRequestHandler<GetAdminServiceReportsQuery, PagedResult<AdminReportDto>>,
    IRequestHandler<CloseServiceReportCommand, AdminReportDto>,
    IRequestHandler<GetAdminDocumentsQuery, PagedResult<AdminDocumentDto>>,
    IRequestHandler<GetDocumentFileQuery, DocumentFile>,
    IRequestHandler<DecideDocumentCommand, AdminDocumentDto>,
    IRequestHandler<GetAdminCategoriesQuery, IReadOnlyList<CategoryDto>>,
    IRequestHandler<SaveCategoryCommand, CategoryDto>,
    IRequestHandler<UpdateServicesSettingsCommand, ServicesSettingsDto>
{
    // --- Tableau de bord ---------------------------------------------------------------------

    public async Task<ServicesOverviewDto> Handle(GetServicesOverviewQuery request, CancellationToken ct)
    {
        var settings = await ServicesData.SettingsAsync(db, ct);
        var now = clock.GetUtcNow();
        var published = db.Reviews.Where(r => r.Status == ReviewStatus.Published);
        return new ServicesOverviewDto(
            await db.Providers.CountAsync(p => p.Status == ProviderStatus.PendingReview, ct),
            await db.Offerings.CountAsync(o => o.HiddenReason == HiddenReason.AlertKeyword || o.HiddenReason == HiddenReason.Reports, ct),
            await db.Reviews.CountAsync(r => r.HiddenReason == HiddenReason.Reports, ct),
            await db.Reports.CountAsync(r => !r.Handled, ct),
            await db.Documents.CountAsync(d => d.Status == DocumentStatus.Pending, ct),
            await db.Providers.CountAsync(p => p.HiddenReason != HiddenReason.None, ct),
            await db.Providers.CountAsync(p => p.Status == ProviderStatus.Active && p.HiddenReason == HiddenReason.None, ct),
            await db.Providers.CountAsync(ct),
            await db.Requests.CountAsync(r => r.CreatedAt > now.AddDays(-7), ct),
            await db.Requests.CountAsync(r => r.CompletedAt > now.AddDays(-30), ct),
            await published.CountAsync(ct),
            Math.Round(await published.AverageAsync(r => (double?)r.Rating, ct) ?? 0, 2),
            new ServicesSettingsDto(settings.AlertKeywords, settings.ReportThreshold, settings.DailyRequestLimit));
    }

    public async Task<ServicesStatsDto> Handle(GetServicesStatsQuery request, CancellationToken ct)
    {
        await ServicesData.EnsureCatalogAsync(db, ct);
        var counts = await ServiceSearchHandlers.ActiveProvidersByCategoryAsync(db, ct);
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var byCategory = categories.Values.Where(c => c.ParentId is not null && counts.ContainsKey(c.Id))
            .Select(c => new CategoryCountDto(c.Id, c.Name, c.Icon, categories.GetValueOrDefault(c.ParentId!.Value)?.Name, counts[c.Id]))
            .OrderByDescending(c => c.Providers).ThenBy(c => c.Name).ToList();
        var byCity = (await db.Providers.AsNoTracking().Where(p => p.Status == ProviderStatus.Active && p.HiddenReason == HiddenReason.None)
                .GroupBy(p => new { p.City, p.Country }).Select(g => new { g.Key.City, g.Key.Country, Count = g.Count() }).ToListAsync(ct))
            .OrderByDescending(x => x.Count).Take(20).Select(x => new CityCountDto(x.City, x.Country, x.Count)).ToList();

        // 12 dernières semaines (lundi au dimanche, UTC)
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var first = monday.AddDays(-7 * 11);
        var since = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rows = await db.Requests.AsNoTracking().Where(r => r.CreatedAt >= since).Select(r => new { r.CreatedAt, r.Status, r.RespondedAt }).ToListAsync(ct);
        var weeks = Enumerable.Range(0, 12).Select(i => first.AddDays(7 * i)).Select(week =>
        {
            var inWeek = rows.Where(r => DateOnly.FromDateTime(r.CreatedAt.UtcDateTime) >= week && DateOnly.FromDateTime(r.CreatedAt.UtcDateTime) < week.AddDays(7)).ToList();
            return new WeekCountDto(week, inWeek.Count, inWeek.Count(r => r.Status is RequestStatus.Accepted or RequestStatus.Completed),
                inWeek.Count(r => r.Status == RequestStatus.Completed));
        }).ToList();
        return new ServicesStatsDto(byCategory, byCity, weeks);
    }

    // --- Prestataires ------------------------------------------------------------------------

    public async Task<PagedResult<AdminProviderDto>> Handle(GetAdminProvidersQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var query = db.Providers.AsNoTracking();
        query = request.View switch
        {
            ProviderView.Review => query.Where(p => p.Status == ProviderStatus.PendingReview || p.HiddenReason == HiddenReason.AlertKeyword || p.HiddenReason == HiddenReason.Reports),
            ProviderView.Pending => query.Where(p => p.Status == ProviderStatus.PendingReview),
            ProviderView.Active => query.Where(p => p.Status == ProviderStatus.Active && p.HiddenReason == HiddenReason.None),
            ProviderView.Hidden => query.Where(p => p.HiddenReason != HiddenReason.None),
            ProviderView.Suspended => query.Where(p => p.Status == ProviderStatus.Suspended),
            ProviderView.Draft => query.Where(p => p.Status == ProviderStatus.Draft),
            _ => query
        };
        if (Text.Like(request.Q) is { } q)
        {
            var pattern = ServicesData.EscapeLike(q);
            query = query.Where(p => EF.Functions.ILike(p.DisplayName, pattern, "\\") || EF.Functions.ILike(p.City, pattern, "\\"));
        }

        var total = await query.CountAsync(ct);
        // À valider : les plus anciens envois d'abord
        var ordered = request.View is ProviderView.Review or ProviderView.Pending
            ? query.OrderBy(p => p.SubmittedAt ?? p.UpdatedAt)
            : query.OrderByDescending(p => p.UpdatedAt);
        var page = await ordered.Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminProviderDto>(await ProvidersAsync(page, ct), total, skip);
    }

    public async Task<AdminProviderDetailDto> Handle(GetAdminProviderQuery request, CancellationToken ct) =>
        await DetailAsync(await ProviderAsync(request.Id, ct), ct);

    public Task<AdminProviderDetailDto> Handle(ApproveProviderCommand request, CancellationToken ct) =>
        DecideProviderAsync(request.Id, p => p.Approve(clock.GetUtcNow()), "Profil prestataire validé", p =>
            ServicesData.NotifyAsync(bus, p.UserId, "Votre profil prestataire est en ligne",
                "Les membres peuvent maintenant vous trouver et vous contacter.", null, ct), ct);

    public Task<AdminProviderDetailDto> Handle(RejectProviderCommand request, CancellationToken ct) =>
        DecideProviderAsync(request.Id, p => p.Reject(request.Reason), $"Profil prestataire refusé : {request.Reason}", p =>
            ServicesData.NotifyAsync(bus, p.UserId, "Votre profil prestataire doit être corrigé", request.Reason, null, ct), ct);

    public Task<AdminProviderDetailDto> Handle(SuspendProviderCommand request, CancellationToken ct) =>
        DecideProviderAsync(request.Id, p => p.Suspend(request.Reason), $"Prestataire suspendu : {request.Reason}", p =>
            ServicesData.NotifyAsync(bus, p.UserId, "Votre profil prestataire est suspendu", request.Reason, null, ct), ct);

    public Task<AdminProviderDetailDto> Handle(ReinstateProviderCommand request, CancellationToken ct) =>
        DecideProviderAsync(request.Id, p => p.Reinstate(), "Prestataire rétabli", _ => Task.CompletedTask, ct);

    public Task<AdminProviderDetailDto> Handle(RevokeVerificationCommand request, CancellationToken ct) =>
        DecideProviderAsync(request.Id, p => p.RevokeVerification(), "Vérification du prestataire retirée", _ => Task.CompletedTask, ct);

    // --- Offres ------------------------------------------------------------------------------

    public async Task<PagedResult<AdminOfferingDto>> Handle(GetAdminOfferingsQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var query = db.Offerings.AsNoTracking();
        query = request.View switch
        {
            ModerationView.Review => query.Where(o => o.HiddenReason == HiddenReason.AlertKeyword || o.HiddenReason == HiddenReason.Reports),
            ModerationView.Hidden => query.Where(o => o.HiddenReason != HiddenReason.None),
            _ => query
        };
        if (Text.Like(request.Q) is { } q)
        {
            var pattern = ServicesData.EscapeLike(q);
            query = query.Where(o => EF.Functions.ILike(o.Title, pattern, "\\") || EF.Functions.ILike(o.Description, pattern, "\\"));
        }

        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(o => o.UpdatedAt).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminOfferingDto>(await OfferingsAsync(page, ct), total, skip);
    }

    public async Task<AdminOfferingDto> Handle(SetOfferingVisibilityCommand request, CancellationToken ct)
    {
        var offering = await db.Offerings.FirstOrDefaultAsync(o => o.Id == request.Id, ct) ?? throw new NotFoundException("Offre");
        if (request.Visible)
        {
            offering.Show();
        }
        else
        {
            offering.Hide(RequireReason(request.Reason));
        }

        await CloseReportsAsync(ReportTarget.Offering, offering.Id, request.Visible ? "Offre réaffichée" : "Offre masquée", ct);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync(request.Visible ? "Offre de service réaffichée" : "Offre de service masquée", "ServiceOffering", offering.Id,
            $"{offering.Title}{(request.Visible ? "" : $" — {request.Reason}")}", ct);
        return (await OfferingsAsync([offering], ct))[0];
    }

    // --- Avis --------------------------------------------------------------------------------

    public async Task<PagedResult<AdminReviewDto>> Handle(GetAdminReviewsQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var query = db.Reviews.AsNoTracking();
        query = request.View switch
        {
            ModerationView.Review => query.Where(r => r.HiddenReason == HiddenReason.Reports || (r.ReportCount > 0 && r.Status == ReviewStatus.Published)),
            ModerationView.Hidden => query.Where(r => r.Status == ReviewStatus.Hidden),
            _ => query
        };
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(r => r.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminReviewDto>(await ReviewsAsync(page, ct), total, skip);
    }

    /// <summary>Masquage d'un avis : motif obligatoire, conservé avec l'avis (jamais de suppression).</summary>
    public async Task<AdminReviewDto> Handle(SetReviewVisibilityCommand request, CancellationToken ct)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == request.Id, ct) ?? throw new NotFoundException("Avis");
        if (request.Visible)
        {
            review.Show();
        }
        else
        {
            review.Hide(request.Reason ?? string.Empty);
        }

        await CloseReportsAsync(ReportTarget.Review, review.Id, request.Visible ? "Avis maintenu" : "Avis masqué", ct);
        await db.SaveChangesAsync(ct);
        await ServicesData.RefreshRatingAsync(db, review.ProviderId, ct);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync(request.Visible ? "Avis de service réaffiché" : "Avis de service masqué", "ServiceReview", review.Id,
            $"{review.Rating}/5{(request.Visible ? "" : $" — {review.ModerationNote}")}", ct);
        return (await ReviewsAsync([review], ct))[0];
    }

    // --- Signalements ------------------------------------------------------------------------

    public async Task<PagedResult<AdminReportDto>> Handle(GetAdminServiceReportsQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var query = db.Reports.AsNoTracking().Where(r => r.Handled == request.Handled);
        var total = await query.CountAsync(ct);
        var page = await (request.Handled ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt)).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminReportDto>(await ReportsAsync(page, ct), total, skip);
    }

    public async Task<AdminReportDto> Handle(CloseServiceReportCommand request, CancellationToken ct)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == request.Id, ct) ?? throw new NotFoundException("Signalement");
        report.Close(string.IsNullOrWhiteSpace(request.Decision) ? "Classé sans suite" : request.Decision);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Signalement de service traité", "ServiceReport", report.Id, report.Decision ?? "", ct);
        return (await ReportsAsync([report], ct))[0];
    }

    // --- Justificatifs -----------------------------------------------------------------------

    public async Task<PagedResult<AdminDocumentDto>> Handle(GetAdminDocumentsQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var query = db.Documents.AsNoTracking().Where(d => d.Status == request.Status);
        var total = await query.CountAsync(ct);
        var page = await (request.Status == DocumentStatus.Pending ? query.OrderBy(d => d.UploadedAt) : query.OrderByDescending(d => d.ReviewedAt))
            .Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminDocumentDto>(await DocumentsAsync(page, ct), total, skip);
    }

    public async Task<DocumentFile> Handle(GetDocumentFileQuery request, CancellationToken ct)
    {
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.Id, ct) ?? throw new NotFoundException("Justificatif");
        var file = await media.OpenAttachmentAsync(document.FileName, ct) ?? throw new NotFoundException("Fichier");
        await admin.LogAsync("Justificatif consulté", "ProviderDocument", document.Id, document.OriginalName, ct);
        return new DocumentFile(file.Content, file.Length, file.ContentType, document.OriginalName);
    }

    /// <summary>Validation : identité → badge « Identité vérifiée » ; diplôme ou registre + SIRET / IFU → « Pro vérifié ».</summary>
    public async Task<AdminDocumentDto> Handle(DecideDocumentCommand request, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == request.Id, ct) ?? throw new NotFoundException("Justificatif");
        var provider = await ProviderAsync(document.ProviderId, ct);
        var now = clock.GetUtcNow();
        string message;
        if (request.Approve)
        {
            document.Approve(now);
            if (document.Kind == DocumentKind.Identity)
            {
                provider.Verify(VerificationLevel.Identity, now);
                message = "Votre identité est vérifiée.";
            }
            else if (provider.HasBusinessNumber)
            {
                provider.Verify(VerificationLevel.Pro, now);
                message = "Votre justificatif est validé : vous avez le badge « Pro vérifié ».";
            }
            else
            {
                message = "Votre justificatif est validé. Ajoutez votre SIRET ou votre IFU pour obtenir le badge « Pro vérifié ».";
            }
        }
        else
        {
            document.Reject(request.Reason ?? string.Empty, now);
            message = $"Votre justificatif n'a pas pu être validé : {document.ReviewNote}";
        }

        await db.SaveChangesAsync(ct);
        await admin.LogAsync(request.Approve ? "Justificatif validé" : "Justificatif refusé", "ProviderDocument", document.Id,
            $"{provider.DisplayName} — {document.Kind}{(request.Approve ? "" : $" — {document.ReviewNote}")}", ct);
        await ServicesData.NotifyAsync(bus, provider.UserId, "Vérification de votre profil prestataire", message, null, ct);
        return (await DocumentsAsync([document], ct))[0];
    }

    // --- Catégories et réglages --------------------------------------------------------------

    public async Task<IReadOnlyList<CategoryDto>> Handle(GetAdminCategoriesQuery request, CancellationToken ct)
    {
        await ServicesData.EnsureCatalogAsync(db, ct);
        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
        return ServiceSearchHandlers.Tree(categories, await ServiceSearchHandlers.ActiveProvidersByCategoryAsync(db, ct));
    }

    public async Task<CategoryDto> Handle(SaveCategoryCommand request, CancellationToken ct)
    {
        await ServicesData.EnsureCatalogAsync(db, ct);
        ServiceCategory? parent = null;
        if (request.ParentId is { } parentId)
        {
            parent = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == parentId, ct) ?? throw new NotFoundException("Catégorie parente");
            if (parent.ParentId is not null)
            {
                throw new DomainException("Deux niveaux au plus : choisissez une catégorie principale comme parente.");
            }
        }

        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();
        if (await db.Categories.AnyAsync(c => c.Slug == slug && c.Id != request.Id, ct))
        {
            throw new DomainException("Cet identifiant est déjà utilisé.");
        }

        ServiceCategory category;
        if (request.Id is { } id)
        {
            category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Catégorie");
            if (request.ParentId is not null && await db.Categories.AnyAsync(c => c.ParentId == id, ct))
            {
                throw new DomainException("Cette catégorie a des sous-catégories : elle doit rester une catégorie principale.");
            }

            category.Update(slug, request.Name, request.Icon, request.ParentId, request.SortOrder, request.IsActive, request.RequiresQualification);
        }
        else
        {
            category = ServiceCategory.Create(slug, request.Name, request.Icon, request.ParentId, request.SortOrder, request.RequiresQualification);
            if (!request.IsActive)
            {
                category.Update(slug, request.Name, request.Icon, request.ParentId, request.SortOrder, false, request.RequiresQualification);
            }

            db.Categories.Add(category);
        }

        await db.SaveChangesAsync(ct);
        await admin.LogAsync(request.Id is null ? "Catégorie de service créée" : "Catégorie de service modifiée", "ServiceCategory", category.Id,
            $"{category.Name}{(category.IsActive ? "" : " (désactivée)")}{(category.RequiresQualification ? " — métier réglementé" : "")}", ct);
        var counts = await ServiceSearchHandlers.ActiveProvidersByCategoryAsync(db, ct);
        return new CategoryDto(category.Id, category.Slug, category.Name, category.Icon, category.ParentId, category.SortOrder, category.IsActive,
            category.RequiresQualification, counts.GetValueOrDefault(category.Id), []);
    }

    public async Task<ServicesSettingsDto> Handle(UpdateServicesSettingsCommand request, CancellationToken ct)
    {
        await ServicesData.EnsureCatalogAsync(db, ct);
        var settings = await db.Settings.FirstAsync(ct);
        settings.Update(request.AlertKeywords ?? [], request.ReportThreshold, request.DailyRequestLimit);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Réglages des services modifiés", "ServicesSettings", null,
            $"{settings.AlertKeywords.Count} mot(s)-clé(s), masquage à {settings.ReportThreshold} signalement(s), {settings.DailyRequestLimit} demande(s) par jour", ct);
        return new ServicesSettingsDto(settings.AlertKeywords, settings.ReportThreshold, settings.DailyRequestLimit);
    }

    // --- Outils ------------------------------------------------------------------------------

    private async Task<ProviderProfile> ProviderAsync(Guid id, CancellationToken ct) =>
        await db.Providers.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Prestataire");

    private async Task<AdminProviderDetailDto> DecideProviderAsync(Guid id, Action<ProviderProfile> decide, string action,
        Func<ProviderProfile, Task> notify, CancellationToken ct)
    {
        var provider = await ProviderAsync(id, ct);
        decide(provider);
        await CloseReportsAsync(ReportTarget.Profile, provider.Id, action, ct);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync(action, "ProviderProfile", provider.Id, provider.DisplayName, ct);
        await notify(provider);
        return await DetailAsync(provider, ct);
    }

    private async Task CloseReportsAsync(ReportTarget target, Guid id, string decision, CancellationToken ct)
    {
        foreach (var report in await db.Reports.Where(r => r.TargetType == target && r.TargetId == id && !r.Handled).ToListAsync(ct))
        {
            report.Close(decision);
        }
    }

    private static string RequireReason(string? reason)
    {
        var text = (reason ?? string.Empty).Trim();
        return text.Length >= 3 ? text : throw new DomainException("Indiquez le motif.");
    }

    private async Task<List<AdminProviderDto>> ProvidersAsync(IReadOnlyList<ProviderProfile> providers, CancellationToken ct)
    {
        var ids = providers.Select(p => p.Id).ToList();
        var offerings = await db.Offerings.AsNoTracking().Where(o => ids.Contains(o.ProviderId)).GroupBy(o => o.ProviderId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var documents = await db.Documents.AsNoTracking().Where(d => ids.Contains(d.ProviderId) && d.Status == DocumentStatus.Pending).GroupBy(d => d.ProviderId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var names = await identity.GetNamesAsync(providers.Select(p => p.UserId).Distinct(), ct);
        return providers.Select(p => new AdminProviderDto(p.Id, p.UserId, names.GetValueOrDefault(p.UserId) ?? "Membre supprimé", p.DisplayName, p.PhotoPath,
            p.City, p.Country, p.Status, p.HiddenReason, p.AlertKeyword, p.ReportCount, p.VerificationLevel, p.LegalStatus, offerings.GetValueOrDefault(p.Id),
            p.RatingAverage, p.RatingCount, p.SubmittedAt, p.ApprovedAt, p.CreatedAt, documents.GetValueOrDefault(p.Id))).ToList();
    }

    private async Task<AdminProviderDetailDto> DetailAsync(ProviderProfile p, CancellationToken ct)
    {
        var summary = (await ProvidersAsync([p], ct))[0];
        var offerings = await db.Offerings.AsNoTracking().Where(o => o.ProviderId == p.Id).OrderBy(o => o.CreatedAt).ToListAsync(ct);
        var categoryIds = offerings.Select(o => o.CategoryId).Distinct().ToList();
        var categories = await db.Categories.AsNoTracking().Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var documents = await db.Documents.AsNoTracking().Where(d => d.ProviderId == p.Id).OrderByDescending(d => d.UploadedAt).ToListAsync(ct);
        var offeringIds = offerings.Select(o => o.Id).ToList();
        var reports = await db.Reports.AsNoTracking()
            .Where(r => (r.TargetType == ReportTarget.Profile && r.TargetId == p.Id) || (r.TargetType == ReportTarget.Offering && offeringIds.Contains(r.TargetId)))
            .OrderByDescending(r => r.CreatedAt).Take(50).ToListAsync(ct);
        return new AdminProviderDetailDto(summary, p.Bio, p.Phone, p.PhoneIsPublic, p.ContactPreference, p.Siret, p.Ifu, p.PostalCode, p.Latitude, p.Longitude,
            p.ServiceRadiusKm, p.ServesAtHome, p.HasPremises, p.QualificationDeclared, categories.Values.Any(c => c.RequiresQualification), p.ReviewNote,
            p.ResponseRate, await db.Requests.CountAsync(r => r.ProviderId == p.Id, ct),
            await db.Requests.CountAsync(r => r.ProviderId == p.Id && r.Status == RequestStatus.Completed, ct),
            offerings.Select(o => o.ToDto(categories)).ToList(), documents.Select(d => d.ToDto()).ToList(), await ReportsAsync(reports, ct));
    }

    private async Task<List<AdminOfferingDto>> OfferingsAsync(IReadOnlyList<ServiceOffering> offerings, CancellationToken ct)
    {
        var providerIds = offerings.Select(o => o.ProviderId).Distinct().ToList();
        var providers = await db.Providers.AsNoTracking().Where(p => providerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var categoryIds = offerings.Select(o => o.CategoryId).Distinct().ToList();
        var categories = await db.Categories.AsNoTracking().Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        return offerings.Select(o => new AdminOfferingDto(o.ToDto(categories), o.ProviderId, providers.GetValueOrDefault(o.ProviderId)?.DisplayName ?? "—",
            providers.GetValueOrDefault(o.ProviderId)?.Status ?? ProviderStatus.Draft, o.ModerationNote, o.ReportCount, o.CreatedAt, o.UpdatedAt)).ToList();
    }

    private async Task<List<AdminReviewDto>> ReviewsAsync(IReadOnlyList<Review> reviews, CancellationToken ct)
    {
        var providerIds = reviews.Select(r => r.ProviderId).Distinct().ToList();
        var providers = await db.Providers.AsNoTracking().Where(p => providerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);
        var names = await identity.GetNamesAsync(reviews.Select(r => r.AuthorId).Distinct(), ct);
        return reviews.Select(r => new AdminReviewDto(r.Id, r.ProviderId, providers.GetValueOrDefault(r.ProviderId) ?? "—",
            names.GetValueOrDefault(r.AuthorId) ?? "Membre supprimé", r.Rating, r.Comment, r.CreatedAt, r.Status, r.HiddenReason, r.ModerationNote,
            r.ReportCount, r.ProviderReply)).ToList();
    }

    private async Task<List<AdminReportDto>> ReportsAsync(IReadOnlyList<ServiceReport> reports, CancellationToken ct)
    {
        var ids = reports.Select(r => r.TargetId).Distinct().ToList();
        var profiles = await db.Providers.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);
        var offerings = await db.Offerings.AsNoTracking().Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => (o.Title, o.ProviderId), ct);
        var reviews = await db.Reviews.AsNoTracking().Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => (r.Rating, r.Comment, r.ProviderId), ct);
        var names = await identity.GetNamesAsync(reports.Select(r => r.AuthorId).Distinct(), ct);
        return reports.Select(r =>
        {
            var (label, provider) = r.TargetType switch
            {
                ReportTarget.Profile => (profiles.GetValueOrDefault(r.TargetId) ?? "Profil supprimé", profiles.ContainsKey(r.TargetId) ? r.TargetId : (Guid?)null),
                ReportTarget.Offering => offerings.TryGetValue(r.TargetId, out var o) ? (o.Title, o.ProviderId) : ("Offre supprimée", (Guid?)null),
                _ => reviews.TryGetValue(r.TargetId, out var v) ? ($"Avis {v.Rating}/5 : {Text.Excerpt(v.Comment, 60)}", v.ProviderId) : ("Avis supprimé", (Guid?)null)
            };
            return new AdminReportDto(r.Id, r.TargetType, r.TargetId, label, provider, names.GetValueOrDefault(r.AuthorId) ?? "Membre supprimé", r.Reason,
                r.CreatedAt, r.Handled, r.Decision);
        }).ToList();
    }

    private async Task<List<AdminDocumentDto>> DocumentsAsync(IReadOnlyList<ProviderDocument> documents, CancellationToken ct)
    {
        var ids = documents.Select(d => d.ProviderId).Distinct().ToList();
        var providers = await db.Providers.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        return documents.Select(d =>
        {
            var p = providers.GetValueOrDefault(d.ProviderId);
            return new AdminDocumentDto(d.ToDto(), d.ProviderId, p?.DisplayName ?? "—", p?.LegalStatus ?? LegalStatus.Individual, p?.Siret, p?.Ifu,
                p?.VerificationLevel ?? VerificationLevel.None);
        }).ToList();
    }
}
