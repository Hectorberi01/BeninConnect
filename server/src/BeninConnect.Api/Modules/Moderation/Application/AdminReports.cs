using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Moderation.Contracts;
using BeninConnect.Modules.Moderation.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Moderation.Application;

public sealed record AdminReportDto(
    Guid Id, ReportTarget TargetType, Guid TargetId, string TargetPreview, string? TargetAuthorName, Guid? PostId, bool TargetExists,
    ReportReason Reason, string Details, string ReporterName, ReportStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt,
    string Resolution, int SameTargetOpenCount);

public sealed record GetAdminReportsQuery(ReportStatus? Status, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminReportDto>>;

/// <summary>Decision : dismiss (classer), hide (masquer la publication), delete (supprimer le contenu), suspend (suspendre l'auteur ou le membre).</summary>
public sealed record ResolveReportCommand(Guid Id, string Decision, string? Note) : ICommand<AdminReportDto>;

/// <summary>File de modération : les décisions s'appliquent via les contrats de Feed, Communities et Identity.</summary>
public sealed class AdminReportHandlers(IModerationDb db, IFeedModule feed, ICommunitiesModule communities, IIdentityModule identity,
    IAdminSession admin, TimeProvider clock) :
    IRequestHandler<GetAdminReportsQuery, PagedResult<AdminReportDto>>,
    IRequestHandler<ResolveReportCommand, AdminReportDto>
{
    public async Task<PagedResult<AdminReportDto>> Handle(GetAdminReportsQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var reports = db.Reports.AsNoTracking();
        if (request.Status is { } status)
        {
            reports = reports.Where(r => r.Status == status);
        }

        var total = await reports.CountAsync(ct);
        var page = await reports.OrderByDescending(r => r.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminReportDto>(await DescribeAsync(page, ct), total, skip);
    }

    public async Task<AdminReportDto> Handle(ResolveReportCommand request, CancellationToken ct)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == request.Id, ct) ?? throw new NotFoundException("Signalement");
        if (report.Status != ReportStatus.Open)
        {
            throw new DomainException("Ce signalement est déjà traité.");
        }

        var note = request.Note?.Trim() ?? string.Empty;
        var now = clock.GetUtcNow();
        var me = await admin.GetAsync(ct);

        var (status, summary) = request.Decision switch
        {
            "dismiss" => (ReportStatus.Dismissed, "Classé sans suite"),
            "hide" => (ReportStatus.Resolved, await HideAsync(report, note, ct)),
            "delete" => (ReportStatus.Resolved, await DeleteAsync(report, ct)),
            "suspend" => (ReportStatus.Resolved, await SuspendAsync(report, note, ct)),
            _ => throw new DomainException("Décision inconnue.")
        };

        var resolution = note.Length > 0 ? $"{summary} — {note}" : summary;

        // Les autres signalements ouverts sur la même cible sont clos avec la même décision.
        var siblings = await db.Reports
            .Where(r => r.TargetType == report.TargetType && r.TargetId == report.TargetId && r.Status == ReportStatus.Open)
            .ToListAsync(ct);
        foreach (var r in siblings)
        {
            r.Close(status, me.Id, resolution, now);
        }

        await db.SaveChangesAsync(ct);
        await admin.LogAsync($"Signalement traité : {summary}", report.TargetType.ToString(), report.TargetId,
            $"{siblings.Count} signalement(s) clos. {note}".Trim(), ct);
        return (await DescribeAsync([report], ct))[0];
    }

    private async Task<string> HideAsync(Report report, string note, CancellationToken ct)
    {
        if (report.TargetType != ReportTarget.Post)
        {
            throw new DomainException("Seule une publication peut être masquée.");
        }

        await feed.HidePostAsync(report.TargetId, note.Length > 0 ? note : $"Signalement : {Label(report.Reason)}", ct);
        return "Publication masquée";
    }

    private async Task<string> DeleteAsync(Report report, CancellationToken ct)
    {
        switch (report.TargetType)
        {
            case ReportTarget.Post:
                await feed.DeletePostAsync(report.TargetId, ct);
                return "Publication supprimée";
            case ReportTarget.Comment:
                await feed.DeleteCommentAsync(report.TargetId, ct);
                return "Commentaire supprimé";
            case ReportTarget.Community:
                await admin.RequireAsync(Permission.CommunitiesManage, ct);
                await communities.DeleteAsync(report.TargetId, ct);
                return "Communauté supprimée";
            default:
                throw new DomainException("Un membre se suspend, il ne se supprime pas.");
        }
    }

    private async Task<string> SuspendAsync(Report report, string note, CancellationToken ct)
    {
        Guid? userId = report.TargetType switch
        {
            ReportTarget.User => report.TargetId,
            ReportTarget.Post => (await feed.GetPostPreviewsAsync([report.TargetId], ct)).GetValueOrDefault(report.TargetId)?.AuthorId,
            ReportTarget.Comment => (await feed.GetCommentPreviewsAsync([report.TargetId], ct)).GetValueOrDefault(report.TargetId)?.AuthorId,
            _ => throw new DomainException("Une communauté ne peut pas être suspendue.")
        };

        var name = await identity.SuspendAsync(userId ?? throw new NotFoundException("Membre"),
            note.Length > 0 ? note : $"Signalement : {Label(report.Reason)}", ct);
        return $"Membre suspendu ({name})";
    }

    internal static string Label(ReportReason reason) => reason switch
    {
        ReportReason.Spam => "spam",
        ReportReason.Harassment => "harcèlement",
        ReportReason.Inappropriate => "contenu inapproprié",
        ReportReason.FalseInformation => "fausse information",
        _ => "autre motif"
    };

    /// <summary>Aperçu des cibles chargé par lots (une requête par type), jamais une par signalement.</summary>
    private async Task<List<AdminReportDto>> DescribeAsync(List<Report> reports, CancellationToken ct)
    {
        var ids = reports.Select(r => r.TargetId).Distinct().ToList();
        var posts = await feed.GetPostPreviewsAsync(ids, ct);
        var comments = await feed.GetCommentPreviewsAsync(ids, ct);
        var groups = await communities.GetInfosAsync(ids, ct);

        var userIds = reports.Select(r => r.ReporterId).Concat(ids)
            .Concat(posts.Values.Select(p => p.AuthorId)).Concat(comments.Values.Select(c => c.AuthorId)).Concat(groups.Values.Select(c => c.OwnerId));
        var names = await identity.GetNamesAsync(userIds, ct);

        var openByTarget = await db.Reports.AsNoTracking().Where(r => ids.Contains(r.TargetId) && r.Status == ReportStatus.Open)
            .GroupBy(r => r.TargetId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return reports.Select(r =>
        {
            var (preview, author, postId, exists) = r.TargetType switch
            {
                ReportTarget.Post when posts.TryGetValue(r.TargetId, out var p) => (Text.Excerpt(p.Content, 120), names.GetValueOrDefault(p.AuthorId), (Guid?)p.Id, true),
                ReportTarget.Comment when comments.TryGetValue(r.TargetId, out var c) => (Text.Excerpt(c.Content, 120), names.GetValueOrDefault(c.AuthorId), (Guid?)c.PostId, true),
                ReportTarget.Community when groups.TryGetValue(r.TargetId, out var c) => (c.Name, names.GetValueOrDefault(c.OwnerId), null, true),
                ReportTarget.User when names.TryGetValue(r.TargetId, out var n) => (n, n, null, true),
                _ => ("Contenu supprimé", null, null, false)
            };
            return new AdminReportDto(r.Id, r.TargetType, r.TargetId, preview, author, postId, exists, r.Reason, r.Details,
                names.GetValueOrDefault(r.ReporterId) ?? "Membre supprimé", r.Status, r.CreatedAt, r.ClosedAt, r.Resolution,
                openByTarget.GetValueOrDefault(r.TargetId));
        }).ToList();
    }
}
