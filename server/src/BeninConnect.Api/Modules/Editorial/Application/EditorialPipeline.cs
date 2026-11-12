using BeninConnect.Modules.Editorial.Domain;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BeninConnect.Modules.Editorial.Application;

public sealed record PipelineReport(int Collected, int Drafted, int Skipped, int Published, int Failed);

/// <summary>Collecte RSS → brouillons du LLM → (validation dans la console) → publication programmée.</summary>
public sealed class EditorialPipeline(
    IEditorialDb db, IFeedReader feeds, ILlmClient llm, IFeedModule feed, IIdentityModule identity,
    EditorialSettings settings, TimeProvider clock, ILogger<EditorialPipeline> log)
{
    public const string SystemPrompt = """
        Tu rédiges les publications du compte officiel BéninConnect, une application communautaire pour la diaspora béninoise.

        Règles :
        - 2 à 4 phrases, en français, ton chaleureux et factuel.
        - Résume avec tes propres mots. Ne recopie jamais de phrases de la source.
        - N'invente aucun fait, chiffre ou citation absent de l'extrait.
        - Termine par une question courte qui invite la communauté à réagir, si c'est naturel.
        - 1 à 3 hashtags pertinents en fin de texte (ex : #Bénin #Diaspora).
        - Ne mets PAS le lien : il est ajouté automatiquement.
        - Si le sujet n'intéresse pas la diaspora ou est sensible (politique partisane, faits divers violents), réponds uniquement : SKIP
        """;

    /// <summary>Lit les sources actives et crée les candidats nouveaux. Ne publie rien.</summary>
    public async Task<int> CollectAsync(CancellationToken ct)
    {
        var total = 0;
        foreach (var source in await db.Sources.Where(s => s.IsEnabled).OrderBy(s => s.Name).ToListAsync(ct))
        {
            var now = clock.GetUtcNow();
            try
            {
                var items = await feeds.ReadAsync(source.Url, settings.MaxItemsPerSource, ct);
                var fresh = items
                    .Select(i => (Item: i, Hash: SourceUrl.Hash(i.Url)))
                    .DistinctBy(x => x.Hash) // même article deux fois dans le flux
                    .ToList();
                var hashes = fresh.Select(x => x.Hash).ToList();
                var known = (await db.Candidates.Where(c => hashes.Contains(c.SourceHash)).Select(c => c.SourceHash).ToListAsync(ct)).ToHashSet();

                var added = 0;
                foreach (var (item, hash) in fresh.Where(x => !known.Contains(x.Hash)))
                {
                    db.Candidates.Add(ContentCandidate.Collect(source.Id, source.Name, item.Url, hash, item.Title, item.Summary, now));
                    added++;
                }

                source.MarkFetched(added, now);
                await db.SaveChangesAsync(ct);
                total += added;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Une source en panne ne bloque pas les autres
                log.LogWarning(ex, "Collecte éditoriale échouée pour {Source}", source.Name);
                DetachPendingCandidates();
                source.MarkFailed(ex.Message, now);
                await db.SaveChangesAsync(ct);
            }
        }

        return total;
    }

    /// <summary>Brouillons pour les candidats collectés. Sans LLM configuré : rien (rédaction dans la console).</summary>
    public async Task<(int Drafted, int Skipped)> DraftAsync(CancellationToken ct)
    {
        if (!llm.IsConfigured)
        {
            return (0, 0);
        }

        var batch = await db.Candidates.Where(c => c.Status == CandidateStatus.Collected)
            .OrderBy(c => c.CollectedAt).Take(settings.DraftBatchSize).ToListAsync(ct);
        var (drafted, skipped) = (0, 0);
        foreach (var c in batch)
        {
            try
            {
                var answer = (await llm.CompleteAsync(SystemPrompt, $"Source : {c.SourceName}\nTitre : {c.SourceTitle}\nExtrait : {c.SourceExcerpt}", ct)).Trim();
                if (IsSkip(answer))
                {
                    c.Skip("Écarté automatiquement : hors sujet ou sensible.");
                    skipped++;
                }
                else
                {
                    c.SetDraft(WithSource(answer, c.SourceName, c.SourceUrl));
                    drafted++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Rédaction échouée pour {Id}", c.Id);
                c.RecordDraftFailure(ex.Message);
            }

            await db.SaveChangesAsync(ct);
        }

        return (drafted, skipped);
    }

    /// <summary>Publie, au nom du compte officiel, les candidats validés dont l'heure est passée.</summary>
    public async Task<(int Published, int Failed)> PublishDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await db.Candidates.Where(c => c.Status == CandidateStatus.Approved && c.ScheduledAt <= now)
            .OrderBy(c => c.ScheduledAt).Take(settings.PublishBatchSize).ToListAsync(ct);
        if (due.Count == 0)
        {
            return (0, 0);
        }

        var author = await OfficialAccountAsync(ct);
        var (published, failed) = (0, 0);
        foreach (var c in due)
        {
            try
            {
                var postId = await feed.PublishAsAsync(author, c.DraftContent!, c.TargetCommunityId, c.Images, ct);
                c.MarkPublished(postId, clock.GetUtcNow());
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Publication éditoriale échouée pour {Id}", c.Id);
                c.MarkPublishFailed(ex.Message);
                failed++;
            }

            // Après chaque publication : pas de double envoi si la suivante échoue
            await db.SaveChangesAsync(ct);
        }

        return (published, failed);
    }

    public Task<Guid> OfficialAccountAsync(CancellationToken ct) =>
        identity.EnsureOfficialAccountAsync(settings.OfficialName, settings.OfficialEmail, settings.OfficialBio, ct);

    /// <summary>« SKIP », « Skip. », « SKIP — hors sujet »… (le modèle n'est pas toujours exact).</summary>
    public static bool IsSkip(string answer) =>
        answer.TrimStart('*', '"', '«', ' ').StartsWith("SKIP", StringComparison.OrdinalIgnoreCase)
        && answer.Length < 80;

    /// <summary>Texte + lien vers la source, raccourci si besoin pour tenir dans une publication.</summary>
    public static string WithSource(string text, string sourceName, string url)
    {
        var footer = $"\n\n📰 {sourceName} : {url}";
        var room = FeedLimits.PostMaxLength - footer.Length;
        var body = text.Trim();
        if (body.Length > room)
        {
            var cut = body[..Math.Max(0, room - 1)];
            var space = cut.LastIndexOf(' ');
            body = (space > room / 2 ? cut[..space] : cut).TrimEnd() + "…";
        }

        return body + footer;
    }

    private void DetachPendingCandidates()
    {
        if (db is DbContext context)
        {
            foreach (var entry in context.ChangeTracker.Entries<ContentCandidate>().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
