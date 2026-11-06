using BeninConnect.Modules.Moderation.Contracts;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Moderation.Domain;

public enum ReportReason
{
    Spam,
    Harassment,
    Inappropriate,
    FalseInformation,
    Other
}

public enum ReportStatus
{
    Open,
    Resolved,
    Dismissed
}

/// <summary>Signalement d'un contenu ou d'un membre par un membre, traité par la modération.</summary>
public sealed class Report
{
    public const int MaxDetails = 500;

    private Report()
    {
    }

    public Guid Id { get; private set; }
    public Guid ReporterId { get; private set; }
    public ReportTarget TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public ReportReason Reason { get; private set; }
    public string Details { get; private set; } = string.Empty;
    public ReportStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedByAdminId { get; private set; }
    public string Resolution { get; private set; } = string.Empty;

    public static Report Create(Guid id, Guid reporterId, ReportTarget target, Guid targetId, ReportReason reason, string? details, DateTimeOffset now)
    {
        var text = details?.Trim() ?? string.Empty;
        if (text.Length > MaxDetails)
        {
            throw new DomainException($"Le détail ne doit pas dépasser {MaxDetails} caractères.");
        }

        return new Report
        {
            Id = id, ReporterId = reporterId, TargetType = target, TargetId = targetId, Reason = reason, Details = text,
            Status = ReportStatus.Open, CreatedAt = now
        };
    }

    public void Close(ReportStatus status, Guid adminId, string resolution, DateTimeOffset now)
    {
        if (Status != ReportStatus.Open)
        {
            throw new DomainException("Ce signalement est déjà traité.");
        }

        Status = status == ReportStatus.Open ? ReportStatus.Resolved : status;
        ClosedByAdminId = adminId;
        Resolution = resolution.Trim();
        ClosedAt = now;
    }
}
