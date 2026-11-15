using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.AppVersionManagement.Domain;

public enum CampaignStatus
{
    /// <summary>Préparée par la détection : attend la validation d'un administrateur.</summary>
    PendingApproval,
    Approved,
    Sent,
    Rejected,

    /// <summary>Remplacée par une détection plus récente avant validation.</summary>
    Superseded
}

public enum RecipientStatus
{
    Pending,
    Sent,

    /// <summary>À jour entre la détection et l'envoi.</summary>
    Skipped,
    Failed
}

/// <summary>Relance des membres dont l'application n'est pas à jour (push + email), envoyée après validation.</summary>
public sealed class UpdateCampaign
{
    private readonly List<CampaignRecipient> _recipients = [];

    private UpdateCampaign()
    {
    }

    public Guid Id { get; private set; }
    public string AppCode { get; private set; } = default!;
    public CampaignStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public int RecipientCount { get; private set; }
    public int RequiredCount { get; private set; }
    public bool SendPush { get; private set; }
    public bool SendEmail { get; private set; }
    public string? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? RejectReason { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public int SentCount { get; private set; }
    public int SkippedCount { get; private set; }
    public int FailedCount { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyList<CampaignRecipient> Recipients => _recipients;

    public static UpdateCampaign Prepare(string appCode, IReadOnlyCollection<CampaignRecipient> recipients, DateTimeOffset now)
    {
        var campaign = new UpdateCampaign
        {
            Id = Guid.NewGuid(), AppCode = appCode, Status = CampaignStatus.PendingApproval, CreatedAt = now,
            RecipientCount = recipients.Count, RequiredCount = recipients.Count(r => r.Required),
        };
        foreach (var r in recipients)
        {
            r.AttachTo(campaign.Id);
            campaign._recipients.Add(r);
        }

        return campaign;
    }

    public void Approve(bool push, bool email, string by, DateTimeOffset now)
    {
        EnsurePending();
        if (!push && !email)
        {
            throw new DomainException("Choisissez au moins un canal : notification push ou email.");
        }

        (Status, SendPush, SendEmail, DecidedBy, DecidedAt) = (CampaignStatus.Approved, push, email, by, now);
    }

    public void Reject(string? reason, string by, DateTimeOffset now)
    {
        EnsurePending();
        (Status, RejectReason, DecidedBy, DecidedAt) = (CampaignStatus.Rejected, reason?.Trim(), by, now);
    }

    public void Supersede() => Status = Status == CampaignStatus.PendingApproval ? CampaignStatus.Superseded : Status;

    /// <summary>Compteurs mis à jour après chaque lot ; envoyée quand plus aucun destinataire n'attend.</summary>
    public void Progress(int sent, int skipped, int failed, bool finished, DateTimeOffset now)
    {
        (SentCount, SkippedCount, FailedCount) = (SentCount + sent, SkippedCount + skipped, FailedCount + failed);
        if (finished)
        {
            (Status, SentAt) = (CampaignStatus.Sent, now);
        }
    }

    private void EnsurePending()
    {
        if (Status != CampaignStatus.PendingApproval)
        {
            throw new ConflictException("Cette relance a déjà été traitée.");
        }
    }
}

public sealed class CampaignRecipient
{
    private CampaignRecipient()
    {
    }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid UserId { get; private set; }
    public string Platform { get; private set; } = default!;
    public string CurrentVersion { get; private set; } = default!;
    public string TargetVersion { get; private set; } = default!;
    public bool Required { get; private set; }
    public RecipientStatus Status { get; private set; }
    public DateTimeOffset? NotifiedAt { get; private set; }

    public static CampaignRecipient For(Guid userId, string platform, string currentVersion, string targetVersion, bool required) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Platform = platform, CurrentVersion = currentVersion, TargetVersion = targetVersion, Required = required,
    };

    internal void AttachTo(Guid campaignId) => CampaignId = campaignId;

    public void Mark(RecipientStatus status, DateTimeOffset now) => (Status, NotifiedAt) = (status, status == RecipientStatus.Sent ? now : NotifiedAt);
}
