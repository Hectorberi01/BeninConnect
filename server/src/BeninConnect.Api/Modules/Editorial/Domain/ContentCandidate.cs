using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Editorial.Domain;

public enum CandidateStatus
{
    Collected,  // récupéré depuis une source, pas encore rédigé
    Drafted,    // brouillon prêt, en attente de validation
    Approved,   // validé et programmé
    Published,
    Rejected,   // écarté par un administrateur
    Skipped,    // écarté automatiquement (hors sujet, sensible, rédaction impossible)
    Failed      // publication impossible après plusieurs essais
}

/// <summary>Publication candidate du compte officiel. Rien n'est publié sans validation d'un administrateur.</summary>
public sealed class ContentCandidate
{
    public const int MaxAttempts = 3;

    private ContentCandidate()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Source RSS d'origine ; null pour une publication écrite dans la console.</summary>
    public Guid? SourceId { get; private set; }

    public string SourceName { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;

    /// <summary>SHA-256 de l'URL normalisée (dédoublonnage) ; vide pour une publication manuelle.</summary>
    public string SourceHash { get; private set; } = string.Empty;

    public string SourceTitle { get; private set; } = string.Empty;

    /// <summary>Extrait de la source : sert seulement d'entrée au LLM, jamais publié.</summary>
    public string SourceExcerpt { get; private set; } = string.Empty;

    public DateTimeOffset CollectedAt { get; private set; }
    public string? DraftContent { get; private set; }
    public int DraftAttempts { get; private set; }
    public Guid? TargetCommunityId { get; private set; }

    /// <summary>Visuels envoyés depuis la console (droits détenus par BéninConnect).</summary>
    public List<string> Images { get; private set; } = [];

    public CandidateStatus Status { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }

    /// <summary>Administrateur qui a validé ou rejeté : responsable éditorial de la publication.</summary>
    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    /// <summary>Motif d'un rejet ou d'un écart automatique.</summary>
    public string? Reason { get; private set; }

    public Guid? PublishedPostId { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public int PublishAttempts { get; private set; }
    public string? LastError { get; private set; }

    public bool IsManual => SourceId is null && SourceHash.Length == 0;

    public static ContentCandidate Collect(Guid sourceId, string sourceName, string url, string hash, string title, string excerpt, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        SourceId = sourceId,
        SourceName = sourceName,
        SourceUrl = url,
        SourceHash = hash,
        SourceTitle = Cut(title.Trim(), 500),
        SourceExcerpt = Cut(excerpt.Trim(), 2000),
        CollectedAt = now,
        Status = CandidateStatus.Collected
    };

    /// <summary>Publication écrite directement dans la console : brouillon, à valider comme les autres.</summary>
    public static ContentCandidate CreateManual(string content, IEnumerable<string> images, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        SourceName = "BéninConnect",
        SourceTitle = Cut(FirstLine(content), 500),
        CollectedAt = now,
        DraftContent = Required(content),
        Images = images.ToList(),
        Status = CandidateStatus.Drafted
    };

    /// <summary>Brouillon du LLM.</summary>
    public void SetDraft(string content)
    {
        EnsureStatus(CandidateStatus.Collected);
        DraftContent = Required(content);
        Status = CandidateStatus.Drafted;
    }

    /// <summary>Texte écrit ou corrigé par un administrateur (y compris un candidat écarté qu'il repêche).</summary>
    public void EditDraft(string content, IEnumerable<string>? images = null)
    {
        EnsureStatus(CandidateStatus.Collected, CandidateStatus.Drafted, CandidateStatus.Skipped, CandidateStatus.Rejected);
        DraftContent = Required(content);
        if (images is not null)
        {
            Images = images.ToList();
        }

        Reason = null;
        Status = CandidateStatus.Drafted;
    }

    /// <summary>Écart automatique (le LLM juge le sujet hors ligne éditoriale).</summary>
    public void Skip(string reason)
    {
        EnsureStatus(CandidateStatus.Collected);
        Reason = Cut(reason, 500);
        Status = CandidateStatus.Skipped;
    }

    /// <summary>Échec du LLM : réessayé, puis écarté après plusieurs échecs (plus aucun appel payant).</summary>
    public void RecordDraftFailure(string error)
    {
        EnsureStatus(CandidateStatus.Collected);
        DraftAttempts++;
        LastError = Cut(error, 1000);
        if (DraftAttempts >= MaxAttempts)
        {
            Reason = "Rédaction automatique impossible : à rédiger dans la console.";
            Status = CandidateStatus.Skipped;
        }
    }

    /// <summary>Validation ; le texte final peut être modifié à ce moment-là. Revalider replanifie.</summary>
    public void Approve(Guid reviewerId, string content, DateTimeOffset scheduledAt, Guid? communityId, IEnumerable<string> images, DateTimeOffset now)
    {
        EnsureStatus(CandidateStatus.Drafted, CandidateStatus.Approved);
        DraftContent = Required(content);
        ScheduledAt = scheduledAt < now ? now : scheduledAt;
        TargetCommunityId = communityId;
        Images = images.ToList();
        ReviewedBy = reviewerId;
        ReviewedAt = now;
        Reason = null;
        Status = CandidateStatus.Approved;
    }

    public void Reject(Guid reviewerId, string? reason, DateTimeOffset now)
    {
        EnsureStatus(CandidateStatus.Collected, CandidateStatus.Drafted, CandidateStatus.Approved, CandidateStatus.Skipped, CandidateStatus.Failed);
        ReviewedBy = reviewerId;
        ReviewedAt = now;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : Cut(reason.Trim(), 500);
        ScheduledAt = null;
        Status = CandidateStatus.Rejected;
    }

    /// <summary>Annule la programmation : retour en attente de validation.</summary>
    public void Unschedule()
    {
        EnsureStatus(CandidateStatus.Approved);
        ScheduledAt = null;
        Status = CandidateStatus.Drafted;
    }

    public void MarkPublished(Guid postId, DateTimeOffset now)
    {
        EnsureStatus(CandidateStatus.Approved);
        PublishedPostId = postId;
        PublishedAt = now;
        LastError = null;
        Status = CandidateStatus.Published;
    }

    public void MarkPublishFailed(string error)
    {
        EnsureStatus(CandidateStatus.Approved);
        PublishAttempts++;
        LastError = Cut(error, 1000);
        if (PublishAttempts >= MaxAttempts)
        {
            Status = CandidateStatus.Failed;
        }
    }

    /// <summary>Nouvelle tentative après un échec de publication.</summary>
    public void Retry(DateTimeOffset now)
    {
        EnsureStatus(CandidateStatus.Failed);
        PublishAttempts = 0;
        ScheduledAt = now;
        Status = CandidateStatus.Approved;
    }

    private void EnsureStatus(params CandidateStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException($"Action impossible pour un contenu « {Label(Status)} ».");
        }
    }

    private static string Required(string content)
    {
        var text = content.Trim();
        return text.Length >= 3 ? text : throw new DomainException("Le texte de la publication est vide.");
    }

    private static string FirstLine(string content) => content.Trim().Split('\n')[0];

    private static string Cut(string value, int max) => value.Length > max ? value[..max] : value;

    public static string Label(CandidateStatus status) => status switch
    {
        CandidateStatus.Collected => "à rédiger",
        CandidateStatus.Drafted => "brouillon",
        CandidateStatus.Approved => "programmé",
        CandidateStatus.Published => "publié",
        CandidateStatus.Rejected => "rejeté",
        CandidateStatus.Skipped => "écarté",
        _ => "en échec"
    };
}
