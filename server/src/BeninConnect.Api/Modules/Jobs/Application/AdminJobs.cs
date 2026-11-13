using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Jobs.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Jobs.Application;

public enum SecretState
{
    None,
    Console,
    Environment,
    Unreadable
}

public sealed record JobSourceDto(
    JobSource Source, string Label, bool IsEnabled, int ImportIntervalMinutes, int MinIntervalMinutes, string? ClientId, bool NeedsClientId,
    bool NeedsSecret, SecretState SecretState, DateTimeOffset? SecretUpdatedAt, IReadOnlyList<JobSearchDefinition> Searches,
    DateTimeOffset? LastRunAt, DateTimeOffset? LastSuccessAt, string? LastError, int LastAdded, int LastUpdated, int LastDuplicates,
    int LastHidden, int LastExpired, int ActiveOffers);

public sealed record JobsAdminOverviewDto(
    bool WorkerEnabled, IReadOnlyList<JobSourceDto> Sources, IReadOnlyList<string> AlertKeywords, int ReportThreshold, IReadOnlyList<LabelCount> Counts);

public sealed record JobReportDto(string Reason, DateTimeOffset CreatedAt, bool Handled);

public sealed record AdminJobOfferDto(
    Guid Id, string Title, string? CompanyName, string LocationLabel, string Country, JobSource Source, string SourceLabel, JobOfferStatus Status,
    HiddenReason HiddenReason, bool NeedsReview, string? AlertKeyword, int ReportCount, bool IsDuplicate, DateTimeOffset PublishedAt,
    DateTimeOffset ImportedAt, string ApplyUrl, string Excerpt, IReadOnlyList<JobReportDto> Reports);

public enum AdminOfferView
{
    Review,
    Hidden,
    Reported,
    All
}

public sealed record GetJobsAdminOverviewQuery : IQuery<JobsAdminOverviewDto>;

public sealed record UpdateJobSourceCommand(JobSource Source, bool IsEnabled, int ImportIntervalMinutes, string? ClientId, IReadOnlyList<JobSearchDefinition>? Searches)
    : ICommand<JobSourceDto>;

/// <summary>Secret vide : supprimé.</summary>
public sealed record SetJobSourceSecretCommand(JobSource Source, string? Secret) : ICommand<JobSourceDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        (Secret?.Length ?? 0) > 500 ? [(nameof(Secret), "Secret trop long.")] : [];
}

public sealed record RunJobImportCommand(JobSource Source) : ICommand<ImportReport>;

public sealed record UpdateJobSettingsCommand(IReadOnlyList<string> AlertKeywords, int ReportThreshold) : ICommand<JobsAdminOverviewDto>;

public sealed record GetAdminJobOffersQuery(AdminOfferView View, string? Q, JobSource? Source, int Skip = 0, int Take = 25)
    : IQuery<PagedResult<AdminJobOfferDto>>;

public sealed record ShowJobOfferCommand(Guid Id) : ICommand<AdminJobOfferDto>;

public sealed record HideJobOfferCommand(Guid Id) : ICommand<AdminJobOfferDto>;

/// <summary>Console : configuration des sources, réglages de modération, file des offres masquées ou signalées.</summary>
public sealed class AdminJobHandlers(
    IJobsDb db, JobImporter importer, ISecretProtector protector, JobsOptions options, IAdminSession admin, TimeProvider clock) :
    IRequestHandler<GetJobsAdminOverviewQuery, JobsAdminOverviewDto>,
    IRequestHandler<UpdateJobSourceCommand, JobSourceDto>,
    IRequestHandler<SetJobSourceSecretCommand, JobSourceDto>,
    IRequestHandler<RunJobImportCommand, ImportReport>,
    IRequestHandler<UpdateJobSettingsCommand, JobsAdminOverviewDto>,
    IRequestHandler<GetAdminJobOffersQuery, PagedResult<AdminJobOfferDto>>,
    IRequestHandler<ShowJobOfferCommand, AdminJobOfferDto>,
    IRequestHandler<HideJobOfferCommand, AdminJobOfferDto>
{
    public async Task<JobsAdminOverviewDto> Handle(GetJobsAdminOverviewQuery request, CancellationToken ct)
    {
        await importer.EnsureConfigAsync(ct);
        // Ordre de priorité des sources (la colonne est du texte : tri en mémoire)
        var sources = (await db.Sources.AsNoTracking().ToListAsync(ct)).OrderBy(s => s.Source).ToList();
        var active = await ActiveCountsAsync(ct);
        var settings = await db.Settings.AsNoTracking().FirstAsync(ct);
        var counts = new List<LabelCount>
        {
            new("Actives", await db.Offers.CountAsync(o => o.Status == JobOfferStatus.Active && o.DuplicateOfId == null, ct)),
            new("À revoir", await db.Offers.CountAsync(o => o.NeedsReview, ct)),
            new("Masquées", await db.Offers.CountAsync(o => o.Status == JobOfferStatus.Hidden, ct)),
            new("Doublons", await db.Offers.CountAsync(o => o.Status == JobOfferStatus.Active && o.DuplicateOfId != null, ct)),
            new("Expirées", await db.Offers.CountAsync(o => o.Status == JobOfferStatus.Expired, ct))
        };
        return new JobsAdminOverviewDto(options.WorkerEnabled, sources.Select(s => ToDto(s, active)).ToList(), settings.AlertKeywords,
            settings.ReportThreshold, counts);
    }

    public async Task<JobSourceDto> Handle(UpdateJobSourceCommand request, CancellationToken ct)
    {
        var config = await ConfigAsync(request.Source, ct);
        config.Configure(request.IsEnabled, request.ImportIntervalMinutes, request.ClientId, request.Searches ?? []);
        if (config.IsEnabled && JobSourceConfig.NeedsSecret(config.Source) && importer.ContextFor(config).Secret is null)
        {
            throw new DomainException("Enregistrez d'abord le secret de cette source.");
        }

        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Source d'offres d'emploi modifiée", "JobSource", null,
            $"{JobSources.Label(config.Source)} — {(config.IsEnabled ? "active" : "désactivée")}, toutes les {config.ImportIntervalMinutes} min, {config.Searches.Count} recherche(s)", ct);
        return ToDto(config, await ActiveCountsAsync(ct));
    }

    public async Task<JobSourceDto> Handle(SetJobSourceSecretCommand request, CancellationToken ct)
    {
        var config = await ConfigAsync(request.Source, ct);
        if (!JobSourceConfig.NeedsSecret(config.Source))
        {
            throw new DomainException("Cette source n'utilise pas de secret.");
        }

        var secret = string.IsNullOrWhiteSpace(request.Secret) ? null : request.Secret.Trim();
        config.SetSecret(secret is null ? null : protector.Protect(secret), clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await admin.LogAsync(secret is null ? "Secret d'une source d'emploi supprimé" : "Secret d'une source d'emploi enregistré", "JobSource", null,
            JobSources.Label(config.Source), ct);
        return ToDto(config, await ActiveCountsAsync(ct));
    }

    /// <summary>Import lancé depuis la console, même si la source est désactivée (test de la configuration).</summary>
    public async Task<ImportReport> Handle(RunJobImportCommand request, CancellationToken ct)
    {
        await importer.EnsureConfigAsync(ct);
        var config = await db.Sources.AsNoTracking().FirstAsync(s => s.Source == request.Source, ct);
        if (request.Source == JobSource.Remotive)
        {
            throw new DomainException("Remotive : import impossible tant que BéninConnect exige un compte (CGU Remotive).");
        }

        if (JobSourceConfig.NeedsClientId(config.Source) && config.ClientId is null)
        {
            throw new DomainException("Renseignez d'abord l'identifiant de la source.");
        }

        if (config.Searches.Count == 0)
        {
            throw new DomainException("Ajoutez au moins une recherche.");
        }

        var report = await importer.RunAsync(request.Source, ct);
        await admin.LogAsync("Import d'offres d'emploi lancé", "JobSource", null,
            $"{JobSources.Label(request.Source)} — {report.Added} ajoutée(s), {report.Updated} mise(s) à jour, {report.Errors.Count} erreur(s)", ct);
        return report;
    }

    public async Task<JobsAdminOverviewDto> Handle(UpdateJobSettingsCommand request, CancellationToken ct)
    {
        await importer.EnsureConfigAsync(ct);
        var settings = await db.Settings.FirstAsync(ct);
        settings.Update(request.AlertKeywords, request.ReportThreshold);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Réglages des offres d'emploi modifiés", "JobSettings", null,
            $"{settings.AlertKeywords.Count} mot(s)-clé(s) d'alerte, masquage à {settings.ReportThreshold} signalement(s)", ct);
        return await Handle(new GetJobsAdminOverviewQuery(), ct);
    }

    public async Task<PagedResult<AdminJobOfferDto>> Handle(GetAdminJobOffersQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var query = db.Offers.AsNoTracking();
        query = request.View switch
        {
            AdminOfferView.Review => query.Where(o => o.NeedsReview),
            AdminOfferView.Hidden => query.Where(o => o.Status == JobOfferStatus.Hidden),
            AdminOfferView.Reported => query.Where(o => o.ReportCount > 0),
            _ => query
        };
        if (Text.Like(request.Q) is { } q)
        {
            var pattern = $"%{q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(o => EF.Functions.ILike(o.Title, pattern, "\\") || EF.Functions.ILike(o.CompanyName ?? "", pattern, "\\"));
        }

        if (request.Source is { } source)
        {
            query = query.Where(o => o.Source == source);
        }

        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(o => o.ImportedAt).ThenByDescending(o => o.Number).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminJobOfferDto>(await DescribeAsync(page, ct), total, skip);
    }

    public Task<AdminJobOfferDto> Handle(ShowJobOfferCommand request, CancellationToken ct) =>
        DecideAsync(request.Id, o => o.Show(clock.GetUtcNow()), "Offre d'emploi réaffichée", ct);

    public Task<AdminJobOfferDto> Handle(HideJobOfferCommand request, CancellationToken ct) =>
        DecideAsync(request.Id, o => o.Hide(HiddenReason.Admin), "Offre d'emploi masquée définitivement", ct);

    private async Task<AdminJobOfferDto> DecideAsync(Guid id, Action<JobOffer> decide, string action, CancellationToken ct)
    {
        var offer = await db.Offers.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Offre");
        decide(offer);
        foreach (var report in await db.Reports.Where(r => r.OfferId == id && !r.Handled).ToListAsync(ct))
        {
            report.MarkHandled();
        }

        await db.SaveChangesAsync(ct);
        await admin.LogAsync(action, "JobOffer", offer.Id, $"{offer.Title} — {offer.SourceLabel}", ct);
        return (await DescribeAsync([offer], ct))[0];
    }

    private async Task<List<AdminJobOfferDto>> DescribeAsync(IReadOnlyList<JobOffer> offers, CancellationToken ct)
    {
        var ids = offers.Select(o => o.Id).ToList();
        var reports = (await db.Reports.AsNoTracking().Where(r => ids.Contains(r.OfferId)).OrderByDescending(r => r.CreatedAt).ToListAsync(ct))
            .ToLookup(r => r.OfferId);
        return offers.Select(o => new AdminJobOfferDto(
            o.Id, o.Title, o.CompanyName, o.LocationLabel, o.Country, o.Source, o.SourceLabel, o.Status, o.HiddenReason, o.NeedsReview,
            o.AlertKeyword, o.ReportCount, o.DuplicateOfId is not null, o.PublishedAt, o.ImportedAt, o.ApplyUrl, Text.Excerpt(o.Description, 400),
            reports[o.Id].Take(20).Select(r => new JobReportDto(r.Reason, r.CreatedAt, r.Handled)).ToList())).ToList();
    }

    private async Task<JobSourceConfig> ConfigAsync(JobSource source, CancellationToken ct)
    {
        await importer.EnsureConfigAsync(ct);
        return await db.Sources.FirstAsync(s => s.Source == source, ct);
    }

    private async Task<Dictionary<JobSource, int>> ActiveCountsAsync(CancellationToken ct) =>
        await db.Offers.Where(o => o.Status == JobOfferStatus.Active && o.DuplicateOfId == null)
            .GroupBy(o => o.Source).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    private JobSourceDto ToDto(JobSourceConfig s, Dictionary<JobSource, int> active)
    {
        var state = options.EnvironmentSecret(s.Source) is not null
            ? SecretState.Environment
            : s.EncryptedSecret is null
                ? SecretState.None
                : protector.Unprotect(s.EncryptedSecret) is null ? SecretState.Unreadable : SecretState.Console;
        return new JobSourceDto(s.Source, JobSources.Label(s.Source), s.IsEnabled, s.ImportIntervalMinutes, JobSources.MinIntervalMinutes(s.Source),
            s.ClientId, JobSourceConfig.NeedsClientId(s.Source), JobSourceConfig.NeedsSecret(s.Source), state, s.SecretUpdatedAt, s.Searches,
            s.LastRunAt, s.LastSuccessAt, s.LastError, s.LastAdded, s.LastUpdated, s.LastDuplicates, s.LastHidden, s.LastExpired,
            active.GetValueOrDefault(s.Source));
    }
}
