using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Moderation.Contracts;
using BeninConnect.Modules.Moderation.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Moderation.Application;

public interface IModerationDb
{
    DbSet<Report> Reports { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record ReportCreatedDto(Guid Id);

/// <summary>Signalement par un membre (publication, commentaire, membre ou communauté).</summary>
public sealed record CreateReportCommand(ReportTarget TargetType, Guid TargetId, ReportReason Reason, string? Details) : ICommand<ReportCreatedDto>;

public sealed class ReportHandlers(IModerationDb db, IFeedModule feed, IIdentityModule identity, ICommunitiesModule communities, ICurrentUser me, TimeProvider clock)
    : IRequestHandler<CreateReportCommand, ReportCreatedDto>
{
    public async Task<ReportCreatedDto> Handle(CreateReportCommand request, CancellationToken ct)
    {
        var ownerId = await OwnerAsync(request.TargetType, request.TargetId, ct);
        if (ownerId is null)
        {
            throw new NotFoundException("Contenu");
        }

        if (ownerId == me.Id)
        {
            throw new DomainException("Vous ne pouvez pas signaler votre propre contenu.");
        }

        if (await db.Reports.AnyAsync(r => r.ReporterId == me.Id && r.TargetType == request.TargetType && r.TargetId == request.TargetId
                                           && r.Status == ReportStatus.Open, ct))
        {
            throw new ConflictException("Vous avez déjà signalé ce contenu ; il est en cours d'examen.");
        }

        var report = Report.Create(Guid.NewGuid(), me.Id, request.TargetType, request.TargetId, request.Reason, request.Details, clock.GetUtcNow());
        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);
        return new ReportCreatedDto(report.Id);
    }

    /// <summary>Auteur ou propriétaire de la cible (null : cible introuvable), via le contrat du module concerné.</summary>
    private async Task<Guid?> OwnerAsync(ReportTarget target, Guid id, CancellationToken ct) => target switch
    {
        ReportTarget.Post => (await feed.GetPostPreviewsAsync([id], ct)).GetValueOrDefault(id)?.AuthorId,
        ReportTarget.Comment => (await feed.GetCommentPreviewsAsync([id], ct)).GetValueOrDefault(id)?.AuthorId,
        ReportTarget.User => await identity.ExistsAsync(id, ct) ? id : null,
        _ => (await communities.GetInfosAsync([id], ct)).GetValueOrDefault(id)?.OwnerId
    };
}
