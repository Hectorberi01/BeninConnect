using System.Text;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Jobs.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Jobs.Application;

public sealed record JobOfferSummaryDto(
    Guid Id, string Title, string? CompanyName, string? CompanyLogoUrl, string LocationLabel, string Country, string? Department,
    ContractType ContractType, string? ContractLabel, bool IsRemote, DateTimeOffset PublishedAt, JobSource Source, string SourceLabel,
    JobOfferStatus Status, bool IsFavorite);

public sealed record JobOfferDto(
    Guid Id, string Title, string Description, string? CompanyName, string? CompanyLogoUrl, string LocationLabel, string Country,
    string? Department, string? PostalCode, double? Latitude, double? Longitude, ContractType ContractType, string? ContractLabel, bool IsRemote,
    string? WorkingTime, string? SalaryLabel, string? SectorLabel, string ApplyUrl, JobSource Source, string SourceLabel,
    DateTimeOffset PublishedAt, DateTimeOffset? ClosingAt, JobOfferStatus Status, bool IsFavorite, bool ReportedByMe);

public sealed record JobPageDto(IReadOnlyList<JobOfferSummaryDto> Items, string? NextCursor);

public sealed record JobFiltersDto(
    int Total, int Remote, IReadOnlyList<LabelCount> Countries, IReadOnlyList<LabelCount> Departments, IReadOnlyList<LabelCount> Contracts,
    IReadOnlyList<LabelCount> Sources);

public sealed record FavoriteDto(Guid OfferId, bool IsFavorite);

public sealed record JobSearchQuery(
    string? Q, string? Country, string? Department, ContractType? Contract, bool? Remote, JobSource? Source, string? Cursor, int Take = 20)
    : IQuery<JobPageDto>;

public sealed record GetJobOfferQuery(Guid Id) : IQuery<JobOfferDto>;

public sealed record GetJobFiltersQuery : IQuery<JobFiltersDto>;

public sealed record GetFavoriteJobsQuery : IQuery<IReadOnlyList<JobOfferSummaryDto>>;

public sealed record SetJobFavoriteCommand(Guid Id, bool Favorite) : ICommand<FavoriteDto>;

public sealed record ReportJobOfferCommand(Guid Id, string Reason) : ICommand<CountDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => Rules.Length(nameof(Reason), Reason, "Le motif", 3, 500);
}

/// <summary>Curseur opaque : date de publication + numéro d'ordre de la dernière offre reçue.</summary>
public static class JobCursor
{
    public static string Encode(DateTimeOffset publishedAt, long number) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publishedAt.UtcTicks}:{number}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static (DateTimeOffset PublishedAt, long Number)? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var b64 = cursor.Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(b64)).Split(':');
            return (new DateTimeOffset(long.Parse(parts[0]), TimeSpan.Zero), long.Parse(parts[1]));
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or OverflowException or ArgumentOutOfRangeException)
        {
            throw new DomainException("Curseur invalide.");
        }
    }
}

/// <summary>Rubrique Emploi des membres : offres visibles (actives, non masquées, hors doublons).</summary>
public sealed class MemberJobHandlers(IJobsDb db, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<JobSearchQuery, JobPageDto>,
    IRequestHandler<GetJobOfferQuery, JobOfferDto>,
    IRequestHandler<GetJobFiltersQuery, JobFiltersDto>,
    IRequestHandler<GetFavoriteJobsQuery, IReadOnlyList<JobOfferSummaryDto>>,
    IRequestHandler<SetJobFavoriteCommand, FavoriteDto>,
    IRequestHandler<ReportJobOfferCommand, CountDto>
{
    private IQueryable<JobOffer> Visible => db.Offers.AsNoTracking().Where(o => o.Status == JobOfferStatus.Active && o.DuplicateOfId == null);

    public async Task<JobPageDto> Handle(JobSearchQuery r, CancellationToken ct)
    {
        var take = Math.Clamp(r.Take, 1, 50);
        var query = Visible;
        if (Text.Like(r.Q) is { } q)
        {
            var pattern = $"%{q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(o => EF.Functions.ILike(o.Title, pattern, "\\") || EF.Functions.ILike(o.CompanyName ?? "", pattern, "\\"));
        }

        if (!string.IsNullOrWhiteSpace(r.Country))
        {
            var country = r.Country.Trim().ToUpperInvariant();
            query = query.Where(o => o.Country == country);
        }

        if (!string.IsNullOrWhiteSpace(r.Department))
        {
            var department = r.Department.Trim().ToUpperInvariant();
            query = query.Where(o => o.Department == department);
        }

        if (r.Contract is { } contract)
        {
            query = query.Where(o => o.ContractType == contract);
        }

        if (r.Remote == true)
        {
            query = query.Where(o => o.IsRemote);
        }

        if (r.Source is { } source)
        {
            query = query.Where(o => o.Source == source);
        }

        if (JobCursor.Decode(r.Cursor) is { } cursor)
        {
            var (after, number) = cursor;
            query = query.Where(o => o.PublishedAt < after || (o.PublishedAt == after && o.Number < number));
        }

        var page = await query.OrderByDescending(o => o.PublishedAt).ThenByDescending(o => o.Number).Take(take + 1).ToListAsync(ct);
        var items = page.Take(take).ToList();
        var favorites = await FavoritesAsync(items.Select(o => o.Id), ct);
        var next = page.Count > take ? JobCursor.Encode(items[^1].PublishedAt, items[^1].Number) : null;
        return new JobPageDto(items.Select(o => Summary(o, favorites.Contains(o.Id))).ToList(), next);
    }

    public async Task<JobOfferDto> Handle(GetJobOfferQuery r, CancellationToken ct)
    {
        // Offre expirée : encore lisible (favoris) ; masquée ou doublon : introuvable
        var offer = await db.Offers.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == r.Id && o.Status != JobOfferStatus.Hidden && o.DuplicateOfId == null, ct)
                    ?? throw new NotFoundException("Offre");
        var favorite = await db.Favorites.AnyAsync(f => f.UserId == me.Id && f.OfferId == offer.Id, ct);
        var reported = await db.Reports.AnyAsync(x => x.UserId == me.Id && x.OfferId == offer.Id, ct);
        return new JobOfferDto(offer.Id, offer.Title, offer.Description, offer.CompanyName, offer.CompanyLogoUrl, offer.LocationLabel, offer.Country,
            offer.Department, offer.PostalCode, offer.Latitude, offer.Longitude, offer.ContractType, offer.ContractLabel, offer.IsRemote,
            offer.WorkingTime, offer.SalaryLabel, offer.SectorLabel, offer.ApplyUrl, offer.Source, offer.SourceLabel, offer.PublishedAt,
            offer.ClosingAt, offer.Status, favorite, reported);
    }

    public async Task<JobFiltersDto> Handle(GetJobFiltersQuery r, CancellationToken ct)
    {
        var total = await Visible.CountAsync(ct);
        var remote = await Visible.CountAsync(o => o.IsRemote, ct);
        var countries = await Visible.Where(o => o.Country != "").GroupBy(o => o.Country)
            .Select(g => new LabelCount(g.Key, g.Count())).OrderByDescending(x => x.Count).ToListAsync(ct);
        var departments = await Visible.Where(o => o.Country == "FR" && o.Department != null).GroupBy(o => o.Department!)
            .Select(g => new LabelCount(g.Key, g.Count())).OrderBy(x => x.Label).ToListAsync(ct);
        var contracts = await Visible.GroupBy(o => o.ContractType).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var sources = await Visible.GroupBy(o => o.Source).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return new JobFiltersDto(total, remote, countries, departments,
            contracts.OrderBy(c => c.Key).Select(c => new LabelCount(c.Key.ToString(), c.Count)).ToList(),
            sources.OrderBy(s => s.Key).Select(s => new LabelCount(s.Key.ToString(), s.Count)).ToList());
    }

    public async Task<IReadOnlyList<JobOfferSummaryDto>> Handle(GetFavoriteJobsQuery r, CancellationToken ct)
    {
        var offers = await db.Favorites.AsNoTracking().Where(f => f.UserId == me.Id)
            .Join(db.Offers.AsNoTracking().Where(o => o.Status != JobOfferStatus.Hidden), f => f.OfferId, o => o.Id, (f, o) => new { f.CreatedAt, Offer = o })
            .OrderByDescending(x => x.CreatedAt).Take(200).Select(x => x.Offer).ToListAsync(ct);
        return offers.Select(o => Summary(o, true)).ToList();
    }

    public async Task<FavoriteDto> Handle(SetJobFavoriteCommand r, CancellationToken ct)
    {
        var existing = await db.Favorites.FirstOrDefaultAsync(f => f.UserId == me.Id && f.OfferId == r.Id, ct);
        if (r.Favorite && existing is null)
        {
            if (!await db.Offers.AnyAsync(o => o.Id == r.Id && o.Status != JobOfferStatus.Hidden, ct))
            {
                throw new NotFoundException("Offre");
            }

            db.Favorites.Add(JobOfferFavorite.Create(me.Id, r.Id, clock.GetUtcNow()));
        }
        else if (!r.Favorite && existing is not null)
        {
            db.Favorites.Remove(existing);
        }

        await db.SaveChangesAsync(ct);
        return new FavoriteDto(r.Id, r.Favorite);
    }

    /// <summary>Un signalement par membre ; au seuil, l'offre est masquée en attendant la revue.</summary>
    public async Task<CountDto> Handle(ReportJobOfferCommand r, CancellationToken ct)
    {
        var offer = await db.Offers.FirstOrDefaultAsync(o => o.Id == r.Id && o.Status != JobOfferStatus.Hidden, ct) ?? throw new NotFoundException("Offre");
        if (await db.Reports.AnyAsync(x => x.OfferId == offer.Id && x.UserId == me.Id, ct))
        {
            throw new ConflictException("Vous avez déjà signalé cette offre.");
        }

        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(ct) ?? JobSettings.CreateDefault();
        db.Reports.Add(JobOfferReport.Create(offer.Id, me.Id, r.Reason, clock.GetUtcNow()));
        offer.AddReport(settings.ReportThreshold);
        await db.SaveChangesAsync(ct);
        return new CountDto(offer.ReportCount);
    }

    private async Task<HashSet<Guid>> FavoritesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.ToList();
        return list.Count == 0
            ? []
            : (await db.Favorites.AsNoTracking().Where(f => f.UserId == me.Id && list.Contains(f.OfferId)).Select(f => f.OfferId).ToListAsync(ct)).ToHashSet();
    }

    internal static JobOfferSummaryDto Summary(JobOffer o, bool favorite) =>
        new(o.Id, o.Title, o.CompanyName, o.CompanyLogoUrl, o.LocationLabel, o.Country, o.Department, o.ContractType, o.ContractLabel, o.IsRemote,
            o.PublishedAt, o.Source, o.SourceLabel, o.Status, favorite);
}

/// <summary>Compte supprimé : favoris et signalements retirés.</summary>
public sealed class DeleteMemberJobData(IJobsDb db) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct)
    {
        await db.Favorites.Where(f => f.UserId == e.UserId).ExecuteDeleteAsync(ct);
        await db.Reports.Where(r => r.UserId == e.UserId).ExecuteDeleteAsync(ct);
    }
}
