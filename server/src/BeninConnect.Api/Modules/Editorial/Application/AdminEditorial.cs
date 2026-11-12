using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Editorial.Domain;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Editorial.Application;

public sealed record CandidateDto(
    Guid Id, CandidateStatus Status, bool IsManual, string SourceName, string SourceUrl, string SourceTitle, string SourceExcerpt,
    string? DraftContent, Guid? TargetCommunityId, string? TargetCommunityName, IReadOnlyList<string> Images,
    DateTimeOffset CollectedAt, DateTimeOffset? ScheduledAt, DateTimeOffset? ReviewedAt, string? Reason,
    Guid? PublishedPostId, DateTimeOffset? PublishedAt, int PublishAttempts, string? LastError);

public sealed record SourceDto(
    Guid Id, string Name, string Url, bool IsEnabled, DateTimeOffset CreatedAt, DateTimeOffset? LastFetchedAt, string? LastError, int CollectedCount);

public sealed record EditorialOverviewDto(
    bool AutomationEnabled, bool LlmConfigured, string LlmModel, Guid OfficialAccountId, string OfficialName, int MaxLength,
    IReadOnlyList<LabelCount> Counts, IReadOnlyList<SourceDto> Sources);

public sealed record GetEditorialOverviewQuery : IQuery<EditorialOverviewDto>;

public sealed record GetCandidatesQuery(CandidateStatus? Status, int Skip = 0, int Take = 25) : IQuery<PagedResult<CandidateDto>>;

/// <summary>Publication écrite dans la console (brouillon, à valider comme les autres).</summary>
public sealed record CreateManualDraftCommand(string Content, IReadOnlyList<string>? Images = null) : ICommand<CandidateDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => EditorialRules.Post(Content, Images);
}

/// <summary>Images null : celles du brouillon sont gardées.</summary>
public sealed record EditDraftCommand(Guid Id, string Content, IReadOnlyList<string>? Images = null) : ICommand<CandidateDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => EditorialRules.Post(Content, Images);
}

/// <summary>Photo envoyée depuis la console (visuel dont BéninConnect détient les droits).</summary>
public sealed record UploadEditorialImageCommand(Stream Content, long Length) : ICommand<UploadedImageDto>;

public sealed record UploadedImageDto(string Path);

public static class EditorialRules
{
    public const int MaxImages = 10;

    public static IEnumerable<(string, string)> Post(string content, IReadOnlyList<string>? images) =>
        Rules.Length("Content", content, "La publication", 3, FeedLimits.PostMaxLength)
            .Concat((images ?? []).SelectMany(i => Rules.ImagePath("Images", i)))
            .Concat((images?.Count ?? 0) > MaxImages ? [("Images", $"{MaxImages} photos au plus.")] : []);
}

/// <summary>Validation par un administrateur. ScheduledAt vide : dès que possible.</summary>
public sealed record ApproveCandidateCommand(Guid Id, string Content, DateTimeOffset? ScheduledAt, Guid? CommunityId, IReadOnlyList<string>? Images)
    : ICommand<CandidateDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => EditorialRules.Post(Content, Images);
}

public sealed record RejectCandidateCommand(Guid Id, string? Reason) : ICommand<CandidateDto>;

public sealed record UnscheduleCandidateCommand(Guid Id) : ICommand<CandidateDto>;

public sealed record RetryCandidateCommand(Guid Id) : ICommand<CandidateDto>;

/// <summary>Étape lancée à la main depuis la console : collect, draft ou publish.</summary>
public sealed record RunEditorialStepCommand(string Step) : ICommand<PipelineReport>;

public sealed record AddSourceCommand(string Name, string Url) : ICommand<SourceDto>;

public sealed record UpdateSourceCommand(Guid Id, string Name, string Url, bool IsEnabled) : ICommand<SourceDto>;

public sealed record DeleteSourceCommand(Guid Id) : ICommand<CountDto>;

/// <summary>Console : file éditoriale du compte officiel et sources. Chaque validation est tracée (responsable éditorial).</summary>
public sealed class AdminEditorialHandlers(
    IEditorialDb db, EditorialPipeline pipeline, ILlmClient llm, EditorialSettings settings, ICommunitiesModule communities,
    IAdminSession admin, IMediaModule media, TimeProvider clock) :
    IRequestHandler<GetEditorialOverviewQuery, EditorialOverviewDto>,
    IRequestHandler<GetCandidatesQuery, PagedResult<CandidateDto>>,
    IRequestHandler<CreateManualDraftCommand, CandidateDto>,
    IRequestHandler<EditDraftCommand, CandidateDto>,
    IRequestHandler<ApproveCandidateCommand, CandidateDto>,
    IRequestHandler<RejectCandidateCommand, CandidateDto>,
    IRequestHandler<UnscheduleCandidateCommand, CandidateDto>,
    IRequestHandler<RetryCandidateCommand, CandidateDto>,
    IRequestHandler<RunEditorialStepCommand, PipelineReport>,
    IRequestHandler<AddSourceCommand, SourceDto>,
    IRequestHandler<UpdateSourceCommand, SourceDto>,
    IRequestHandler<DeleteSourceCommand, CountDto>,
    IRequestHandler<UploadEditorialImageCommand, UploadedImageDto>
{
    public async Task<EditorialOverviewDto> Handle(GetEditorialOverviewQuery request, CancellationToken ct)
    {
        var counts = await db.Candidates.GroupBy(c => c.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var sources = await db.Sources.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        return new EditorialOverviewDto(
            settings.Enabled, llm.IsConfigured, llm.Model, await pipeline.OfficialAccountAsync(ct), settings.OfficialName, FeedLimits.PostMaxLength,
            Enum.GetValues<CandidateStatus>().Select(s => new LabelCount(s.ToString(), counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0)).ToList(),
            sources.Select(ToDto).ToList());
    }

    public async Task<PagedResult<CandidateDto>> Handle(GetCandidatesQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var query = db.Candidates.AsNoTracking();
        if (request.Status is { } status)
        {
            query = query.Where(c => c.Status == status);
        }

        // À valider : les plus anciens d'abord ; programmés : par heure ; le reste : les plus récents d'abord
        query = request.Status switch
        {
            CandidateStatus.Drafted or CandidateStatus.Collected => query.OrderBy(c => c.CollectedAt),
            CandidateStatus.Approved => query.OrderBy(c => c.ScheduledAt),
            CandidateStatus.Published => query.OrderByDescending(c => c.PublishedAt),
            _ => query.OrderByDescending(c => c.CollectedAt)
        };

        var total = await query.CountAsync(ct);
        var page = await query.Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<CandidateDto>(await DescribeAsync(page, ct), total, skip);
    }

    public async Task<CandidateDto> Handle(CreateManualDraftCommand request, CancellationToken ct)
    {
        var candidate = ContentCandidate.CreateManual(request.Content, request.Images ?? [], clock.GetUtcNow());
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Brouillon éditorial créé", "Editorial", candidate.Id, candidate.SourceTitle, ct);
        return (await DescribeAsync([candidate], ct))[0];
    }

    public Task<CandidateDto> Handle(EditDraftCommand request, CancellationToken ct) =>
        UpdateAsync(request.Id, c => c.EditDraft(request.Content, request.Images), null, ct);

    public async Task<UploadedImageDto> Handle(UploadEditorialImageCommand request, CancellationToken ct) =>
        new(await media.SaveImageAsync(request.Content, request.Length, ct));

    public async Task<CandidateDto> Handle(ApproveCandidateCommand request, CancellationToken ct)
    {
        if (request.CommunityId is { } id && !(await communities.GetInfosAsync([id], ct)).ContainsKey(id))
        {
            throw new NotFoundException("Communauté");
        }

        var me = await admin.GetAsync(ct);
        var now = clock.GetUtcNow();
        return await UpdateAsync(request.Id,
            c => c.Approve(me.Id, request.Content, request.ScheduledAt ?? now, request.CommunityId, request.Images ?? [], now),
            c => $"Publication programmée le {c.ScheduledAt:dd/MM/yyyy HH:mm} UTC", ct);
    }

    public async Task<CandidateDto> Handle(RejectCandidateCommand request, CancellationToken ct)
    {
        var me = await admin.GetAsync(ct);
        return await UpdateAsync(request.Id, c => c.Reject(me.Id, request.Reason, clock.GetUtcNow()), c => c.Reason ?? "Sans motif", ct);
    }

    public Task<CandidateDto> Handle(UnscheduleCandidateCommand request, CancellationToken ct) =>
        UpdateAsync(request.Id, c => c.Unschedule(), _ => "Programmation annulée", ct);

    public Task<CandidateDto> Handle(RetryCandidateCommand request, CancellationToken ct) =>
        UpdateAsync(request.Id, c => c.Retry(clock.GetUtcNow()), _ => "Nouvelle tentative de publication", ct);

    public async Task<PipelineReport> Handle(RunEditorialStepCommand request, CancellationToken ct)
    {
        var report = request.Step switch
        {
            "collect" => new PipelineReport(await pipeline.CollectAsync(ct), 0, 0, 0, 0),
            "draft" => await DraftAsync(ct),
            "publish" => await PublishAsync(ct),
            _ => throw new DomainException("Étape inconnue (collect, draft ou publish).")
        };
        await admin.LogAsync($"Éditorial : étape « {request.Step} » lancée", "Editorial", null, report.ToString(), ct);
        return report;
    }

    public async Task<SourceDto> Handle(AddSourceCommand request, CancellationToken ct)
    {
        var source = EditorialSource.Create(request.Name, request.Url, clock.GetUtcNow());
        if (await db.Sources.AnyAsync(s => s.Url == source.Url, ct))
        {
            throw new DomainException("Cette source est déjà suivie.");
        }

        db.Sources.Add(source);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Source éditoriale ajoutée", "EditorialSource", source.Id, $"{source.Name} — {source.Url}", ct);
        return ToDto(source);
    }

    public async Task<SourceDto> Handle(UpdateSourceCommand request, CancellationToken ct)
    {
        var source = await db.Sources.FirstOrDefaultAsync(s => s.Id == request.Id, ct) ?? throw new NotFoundException("Source");
        source.Update(request.Name, request.Url, request.IsEnabled);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Source éditoriale modifiée", "EditorialSource", source.Id, $"{source.Name} — {(source.IsEnabled ? "active" : "en pause")}", ct);
        return ToDto(source);
    }

    /// <summary>Les contenus déjà collectés restent (historique, dédoublonnage).</summary>
    public async Task<CountDto> Handle(DeleteSourceCommand request, CancellationToken ct)
    {
        var source = await db.Sources.FirstOrDefaultAsync(s => s.Id == request.Id, ct) ?? throw new NotFoundException("Source");
        db.Sources.Remove(source);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Source éditoriale supprimée", "EditorialSource", source.Id, source.Name, ct);
        return new CountDto(1);
    }

    private async Task<PipelineReport> DraftAsync(CancellationToken ct)
    {
        if (!llm.IsConfigured)
        {
            throw new DomainException("Rédaction automatique non configurée (Editorial__Llm__ApiKey) : rédigez les brouillons dans la console.");
        }

        var (drafted, skipped) = await pipeline.DraftAsync(ct);
        return new PipelineReport(0, drafted, skipped, 0, 0);
    }

    private async Task<PipelineReport> PublishAsync(CancellationToken ct)
    {
        var (published, failed) = await pipeline.PublishDueAsync(ct);
        return new PipelineReport(0, 0, 0, published, failed);
    }

    private async Task<CandidateDto> UpdateAsync(Guid id, Action<ContentCandidate> change, Func<ContentCandidate, string>? audit, CancellationToken ct)
    {
        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Contenu");
        var before = candidate.Status;
        change(candidate);
        await db.SaveChangesAsync(ct);
        if (audit is not null)
        {
            await admin.LogAsync($"Éditorial : {ContentCandidate.Label(before)} → {ContentCandidate.Label(candidate.Status)}", "Editorial", candidate.Id,
                $"{candidate.SourceTitle} — {audit(candidate)}", ct);
        }

        return (await DescribeAsync([candidate], ct))[0];
    }

    private async Task<List<CandidateDto>> DescribeAsync(IReadOnlyList<ContentCandidate> items, CancellationToken ct)
    {
        var ids = items.Where(c => c.TargetCommunityId is not null).Select(c => c.TargetCommunityId!.Value).Distinct().ToList();
        var names = ids.Count > 0 ? await communities.GetInfosAsync(ids, ct) : new Dictionary<Guid, CommunityInfo>();
        return items.Select(c => new CandidateDto(
            c.Id, c.Status, c.IsManual, c.SourceName, c.SourceUrl, c.SourceTitle, c.SourceExcerpt, c.DraftContent,
            c.TargetCommunityId, c.TargetCommunityId is { } t && names.TryGetValue(t, out var info) ? info.Name : null, c.Images,
            c.CollectedAt, c.ScheduledAt, c.ReviewedAt, c.Reason, c.PublishedPostId, c.PublishedAt, c.PublishAttempts, c.LastError)).ToList();
    }

    private static SourceDto ToDto(EditorialSource s) =>
        new(s.Id, s.Name, s.Url, s.IsEnabled, s.CreatedAt, s.LastFetchedAt, s.LastError, s.CollectedCount);
}
