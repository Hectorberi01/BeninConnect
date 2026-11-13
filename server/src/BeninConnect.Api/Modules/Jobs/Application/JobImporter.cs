using BeninConnect.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BeninConnect.Modules.Jobs.Application;

/// <summary>Import d'une source : upsert par page, doublons inter-sources, mots-clés d'alerte, expiration, purge.</summary>
public sealed class JobImporter(
    IJobsDb db, IEnumerable<IJobOfferSource> adapters, ISecretProtector protector, JobsOptions options, TimeProvider clock, ILogger<JobImporter> log)
{
    private const int PageSize = 150;

    /// <summary>Crée les configurations manquantes (désactivées) et la ligne de réglages.</summary>
    public async Task EnsureConfigAsync(CancellationToken ct)
    {
        var existing = await db.Sources.Select(s => s.Source).ToListAsync(ct);
        foreach (var source in Enum.GetValues<JobSource>().Except(existing))
        {
            db.Sources.Add(JobSourceConfig.CreateDefault(source));
        }

        if (!await db.Settings.AnyAsync(ct))
        {
            db.Settings.Add(JobSettings.CreateDefault());
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Créées en même temps par une autre requête
            db.ChangeTracker.Clear();
        }
    }

    public JobSourceContext ContextFor(JobSourceConfig config)
    {
        var secret = options.EnvironmentSecret(config.Source)
                     ?? (config.EncryptedSecret is { } e ? protector.Unprotect(e) : null);
        return new JobSourceContext(config.ClientId, secret);
    }

    /// <summary>Sources activées dont l'intervalle est écoulé.</summary>
    public async Task<IReadOnlyList<JobSource>> DueSourcesAsync(CancellationToken ct)
    {
        await EnsureConfigAsync(ct);
        var now = clock.GetUtcNow();
        var all = await db.Sources.AsNoTracking().ToListAsync(ct);
        return all.Where(s => s.IsDue(now)).Select(s => s.Source).Order().ToList();
    }

    public async Task<ImportReport> RunAsync(JobSource source, CancellationToken ct)
    {
        await EnsureConfigAsync(ct);
        var config = await db.Sources.FirstAsync(s => s.Source == source, ct);
        var adapter = adapters.FirstOrDefault(a => a.Source == source)
                      ?? throw new InvalidOperationException($"Aucun adaptateur pour {source}.");
        var context = ContextFor(config);
        if (JobSourceConfig.NeedsSecret(source) && context.Secret is null)
        {
            config.Failed("Secret manquant ou illisible : saisissez-le dans la console.");
            config.Started(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return new ImportReport(source, 0, 0, 0, 0, 0, 0, [config.LastError!]);
        }

        config.Started(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        var since = config.LastSuccessAt is null ? options.FirstImportDays : options.NextImportDays;
        var searches = config.Searches.ToList();
        var settings = await db.Settings.AsNoTracking().FirstAsync(ct);
        var totals = new Counters();
        var errors = new List<string>();

        foreach (var search in searches)
        {
            var counters = new Counters();
            try
            {
                var page = new List<ImportedOfferData>(PageSize);
                await foreach (var item in adapter.FetchRecentAsync(context, search, since, ct))
                {
                    page.Add(item);
                    if (page.Count == PageSize)
                    {
                        await SavePageAsync(source, search.Key, page, settings.AlertKeywords, counters, ct);
                        page.Clear();
                    }
                }

                if (page.Count > 0)
                {
                    await SavePageAsync(source, search.Key, page, settings.AlertKeywords, counters, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                errors.Add($"{search.Key} : {ex.Message}");
                log.LogWarning(ex, "Emploi : recherche {Source}/{Search} en échec", source, search.Key);
            }

            log.LogInformation(
                "Emploi : {Source}/{Search} lues {Fetched}, ajoutées {Added}, mises à jour {Updated}, doublons {Duplicates}, masquées {Hidden}",
                source, search.Key, counters.Fetched, counters.Added, counters.Updated, counters.Duplicates, counters.Hidden);
            totals.Add(counters);
        }

        var expired = await ExpireAsync(source, adapter, context, ct);
        var purged = await PurgeAsync(ct);
        log.LogInformation("Emploi : {Source} expirées {Expired}, purgées {Purged}", source, expired, purged);

        db.ChangeTracker.Clear();
        config = await db.Sources.FirstAsync(s => s.Source == source, ct);
        var allFailed = searches.Count > 0 && errors.Count == searches.Count;
        if (allFailed)
        {
            config.Failed(string.Join(" | ", errors));
        }
        else
        {
            config.Completed(totals.Added, totals.Updated, totals.Duplicates, totals.Hidden, expired,
                errors.Count > 0 ? string.Join(" | ", errors) : null, clock.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
        return new ImportReport(source, totals.Fetched, totals.Added, totals.Updated, totals.Duplicates, totals.Hidden, expired, errors);
    }

    private async Task SavePageAsync(JobSource source, string searchKey, List<ImportedOfferData> page, IReadOnlyList<string> alerts,
        Counters counters, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var items = page.Where(i => !string.IsNullOrWhiteSpace(i.ExternalId)).DistinctBy(i => i.ExternalId).ToList();
        counters.Fetched += items.Count;
        var ids = items.Select(i => JobText.Cut(i.ExternalId, 200)).ToList();
        var existing = await db.Offers.Where(o => o.Source == source && ids.Contains(o.ExternalId)).ToDictionaryAsync(o => o.ExternalId, ct);

        var created = new List<JobOffer>();
        foreach (var item in items)
        {
            try
            {
                if (existing.TryGetValue(JobText.Cut(item.ExternalId, 200), out var offer))
                {
                    offer.Refresh(item, searchKey, now);
                    counters.Updated++;
                    continue;
                }

                offer = JobOffer.Import(source, item, searchKey, now);
                if (JobText.FindAlert(alerts, offer.Title, offer.Description, offer.CompanyName) is { } keyword)
                {
                    offer.FlagAlert(keyword);
                    counters.Hidden++;
                }

                db.Offers.Add(offer);
                created.Add(offer);
                counters.Added++;
            }
            catch (Exception ex) when (ex is Shared.Domain.DomainException)
            {
                log.LogDebug("Emploi : offre {Source}/{Id} ignorée ({Reason})", source, item.ExternalId, ex.Message);
            }
        }

        counters.Duplicates += await LinkDuplicatesAsync(source, created, ct);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <summary>Même empreinte qu'une offre active d'une autre source : la moins prioritaire devient doublon.</summary>
    private async Task<int> LinkDuplicatesAsync(JobSource source, List<JobOffer> created, CancellationToken ct)
    {
        var candidates = created.Where(o => o.Status == JobOfferStatus.Active).ToList();
        if (candidates.Count == 0)
        {
            return 0;
        }

        var prints = candidates.Select(o => o.Fingerprint).Distinct().ToList();
        var others = await db.Offers
            .Where(o => o.Source != source && o.Status == JobOfferStatus.Active && o.DuplicateOfId == null && prints.Contains(o.Fingerprint))
            .ToListAsync(ct);
        var duplicates = 0;
        foreach (var offer in candidates)
        {
            var other = others.Where(o => o.Fingerprint == offer.Fingerprint).OrderBy(o => o.Source).FirstOrDefault();
            if (other is null)
            {
                continue;
            }

            duplicates++;
            if (other.Source < source)
            {
                offer.MarkDuplicateOf(other.Id);
            }
            else
            {
                foreach (var weaker in others.Where(o => o.Fingerprint == offer.Fingerprint))
                {
                    weaker.MarkDuplicateOf(offer.Id);
                }
            }
        }

        return duplicates;
    }

    /// <summary>Date de clôture passée, ou offre non revue vérifiée chez la source (nombre limité par passage).</summary>
    private async Task<int> ExpireAsync(JobSource source, IJobOfferSource adapter, JobSourceContext context, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var expired = new List<Guid>();
        foreach (var offer in await db.Offers.Where(o => o.Source == source && o.Status == JobOfferStatus.Active && o.ClosingAt != null && o.ClosingAt <= now).ToListAsync(ct))
        {
            if (offer.Expire(now))
            {
                expired.Add(offer.Id);
            }
        }

        var stale = await db.Offers
            .Where(o => o.Source == source && o.Status == JobOfferStatus.Active && o.LastSeenAt < now.AddDays(-options.RecheckAfterDays))
            .OrderBy(o => o.LastSeenAt).Take(options.RechecksPerRun).ToListAsync(ct);
        foreach (var offer in stale)
        {
            bool? exists;
            try
            {
                exists = await adapter.StillExistsAsync(context, offer.ExternalId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogDebug(ex, "Emploi : vérification {Source}/{Id} impossible", source, offer.ExternalId);
                continue;
            }

            var gone = exists == false || (exists is null && offer.LastSeenAt < now.AddDays(-options.UnverifiableExpireAfterDays));
            if (gone && offer.Expire(now))
            {
                expired.Add(offer.Id);
            }
            else if (exists == true)
            {
                offer.Touch(now);
            }
        }

        await db.SaveChangesAsync(ct);
        if (expired.Count > 0)
        {
            // Les doublons d'une offre expirée redeviennent visibles
            foreach (var duplicate in await db.Offers.Where(o => o.DuplicateOfId != null && expired.Contains(o.DuplicateOfId.Value)).ToListAsync(ct))
            {
                duplicate.MarkDuplicateOf(null);
            }

            await db.SaveChangesAsync(ct);
        }

        return expired.Count;
    }

    /// <summary>Offres expirées depuis longtemps : supprimées avec favoris et signalements.</summary>
    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        var limit = clock.GetUtcNow().AddDays(-options.PurgeAfterDays);
        return await db.Offers.Where(o => o.Status == JobOfferStatus.Expired && o.ExpiredAt != null && o.ExpiredAt < limit).ExecuteDeleteAsync(ct);
    }

    private sealed class Counters
    {
        public int Fetched;
        public int Added;
        public int Updated;
        public int Duplicates;
        public int Hidden;

        public void Add(Counters c)
        {
            Fetched += c.Fetched;
            Added += c.Added;
            Updated += c.Updated;
            Duplicates += c.Duplicates;
            Hidden += c.Hidden;
        }
    }
}
