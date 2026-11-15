using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.AppVersionManagement.Domain;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.AppVersionManagement.Application;

public sealed record ReleaseDto(string Platform, string? LatestVersion, string? MinimumSupportedVersion, string StoreUrl, DateTimeOffset? UpdatedAt, string? UpdatedBy);

public sealed record StatusCountsDto(int UpToDate, int UpdateAvailable, int UpdateRequired, int Unknown)
{
    public int Total => UpToDate + UpdateAvailable + UpdateRequired + Unknown;
}

public sealed record VersionShareDto(string Platform, string Version, UpdateStatus Status, int Installations, int Members, DateTimeOffset LastSeenAt);

public sealed record CampaignDto(
    Guid Id, CampaignStatus Status, DateTimeOffset CreatedAt, int RecipientCount, int RequiredCount, bool SendPush, bool SendEmail,
    string? DecidedBy, DateTimeOffset? DecidedAt, string? RejectReason, DateTimeOffset? SentAt, int SentCount, int SkippedCount, int FailedCount);

public sealed record RecipientDto(
    Guid UserId, string Name, string Platform, string CurrentVersion, string TargetVersion, bool Required, RecipientStatus Status, DateTimeOffset? NotifiedAt);

public sealed record CampaignDetailDto(CampaignDto Campaign, IReadOnlyList<RecipientDto> Recipients);

/// <summary>Installations : vues depuis ActiveDays. Members : membres connectés, selon leur installation la plus en retard.</summary>
public sealed record AppVersionsOverviewDto(
    string AppCode, int ActiveDays, IReadOnlyList<ReleaseDto> Releases, StatusCountsDto Installations, StatusCountsDto Members,
    IReadOnlyList<VersionShareDto> Versions, CampaignDto? Pending, CampaignDto? Sending);

public sealed record AdminInstallationDto(
    Guid Id, Guid InstallationId, Guid? UserId, string? MemberName, string Platform, string Version, string BuildNumber, string? OsVersion,
    UpdateStatus Status, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt);

public sealed record GetAppVersionsOverviewQuery : IQuery<AppVersionsOverviewDto>;

public sealed record GetInstallationsQuery(UpdateStatus? Status, string? Platform, Guid? UserId, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminInstallationDto>>;

public sealed record UpdateReleaseCommand(string Platform, string? LatestVersion, string? MinimumSupportedVersion, string StoreUrl) : ICommand<ReleaseDto>;

public sealed record GetCampaignsQuery(int Skip = 0, int Take = 20) : IQuery<PagedResult<CampaignDto>>;

public sealed record GetCampaignQuery(Guid Id) : IQuery<CampaignDetailDto>;

/// <summary>Détection immédiate (même traitement que la tâche quotidienne) ; null : aucun membre à relancer.</summary>
public sealed record DetectOutdatedCommand : ICommand<CampaignDto?>;

public sealed record ApproveCampaignCommand(Guid Id, bool SendPush, bool SendEmail) : ICommand<CampaignDto>;

public sealed record RejectCampaignCommand(Guid Id, string? Reason) : ICommand<CampaignDto>;

/// <summary>Console (permission AppVersionsManage) : versions de référence, parc installé, relances à valider. Chaque décision est journalisée.</summary>
public sealed class AdminAppVersionHandlers(
    IAppVersionsDb db, AppVersionSettings settings, OutdatedDetector detector, IIdentityModule identity, IAdminSession admin, TimeProvider clock) :
    IRequestHandler<GetAppVersionsOverviewQuery, AppVersionsOverviewDto>,
    IRequestHandler<GetInstallationsQuery, PagedResult<AdminInstallationDto>>,
    IRequestHandler<UpdateReleaseCommand, ReleaseDto>,
    IRequestHandler<GetCampaignsQuery, PagedResult<CampaignDto>>,
    IRequestHandler<GetCampaignQuery, CampaignDetailDto>,
    IRequestHandler<DetectOutdatedCommand, CampaignDto?>,
    IRequestHandler<ApproveCampaignCommand, CampaignDto>,
    IRequestHandler<RejectCampaignCommand, CampaignDto>
{
    private DateTimeOffset ActiveSince => clock.GetUtcNow().AddDays(-settings.ActiveDays);

    public async Task<AppVersionsOverviewDto> Handle(GetAppVersionsOverviewQuery q, CancellationToken ct)
    {
        var releases = await AppVersionsData.ReleasesAsync(db, settings, ct);
        var since = ActiveSince;
        var active = db.Installations.AsNoTracking().Where(i => i.AppCode == settings.AppCode && i.LastSeenAt >= since);

        var groups = await active.GroupBy(i => new { i.Platform, i.Version })
            .Select(g => new
            {
                g.Key.Platform, g.Key.Version, Installations = g.Count(),
                Members = g.Where(i => i.UserId != null).Select(i => i.UserId).Distinct().Count(), LastSeenAt = g.Max(i => i.LastSeenAt)
            })
            .ToListAsync(ct);
        var versions = groups
            .Select(g => new VersionShareDto(g.Platform, g.Version, Status(releases, g.Platform, g.Version), g.Installations, g.Members, g.LastSeenAt))
            .OrderBy(v => v.Platform).ThenByDescending(v => v.Version, Comparer<string>.Create(VersionRules.Compare)).ToList();

        var members = await active.Where(i => i.UserId != null)
            .Select(i => new { UserId = i.UserId!.Value, i.Platform, i.Version, i.LastSeenAt }).ToListAsync(ct);
        var memberStatus = AppVersionsData.LatestPerUserAndPlatform(members, m => m.UserId, m => m.Platform, m => m.LastSeenAt)
            .GroupBy(m => m.UserId)
            .Select(g => g.Max(m => Status(releases, m.Platform, m.Version)))
            .ToList();

        var pending = await db.Campaigns.AsNoTracking().Where(c => c.Status == CampaignStatus.PendingApproval).OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        var sending = await db.Campaigns.AsNoTracking().Where(c => c.Status == CampaignStatus.Approved).OrderBy(c => c.DecidedAt).FirstOrDefaultAsync(ct);
        return new AppVersionsOverviewDto(settings.AppCode, settings.ActiveDays,
            releases.Values.Select(ToDto).ToList(),
            Counts(versions.SelectMany(v => Enumerable.Repeat(v.Status, v.Installations))),
            Counts(memberStatus), versions, pending is null ? null : ToDto(pending), sending is null ? null : ToDto(sending));
    }

    public async Task<PagedResult<AdminInstallationDto>> Handle(GetInstallationsQuery q, CancellationToken ct)
    {
        var releases = await AppVersionsData.ReleasesAsync(db, settings, ct);
        var since = ActiveSince;
        var query = db.Installations.AsNoTracking().Where(i => i.AppCode == settings.AppCode && i.LastSeenAt >= since);
        if (q.Platform is { Length: > 0 })
        {
            var platform = VersionRules.Platform(q.Platform);
            query = query.Where(i => i.Platform == platform);
        }

        if (q.UserId is { } userId)
        {
            query = query.Where(i => i.UserId == userId);
        }

        if (q.Status is { } status)
        {
            // Le statut dépend des versions de référence : filtre sur les couples plateforme / version concernés
            var keys = (await query.Select(i => new { i.Platform, i.Version }).Distinct().ToListAsync(ct))
                .Where(k => Status(releases, k.Platform, k.Version) == status).Select(k => k.Platform + "|" + k.Version).ToList();
            query = query.Where(i => keys.Contains(i.Platform + "|" + i.Version));
        }

        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(i => i.LastSeenAt).Skip(Math.Max(0, q.Skip)).Take(Math.Clamp(q.Take, 1, 100)).ToListAsync(ct);
        var names = await identity.GetNamesAsync(page.Where(i => i.UserId != null).Select(i => i.UserId!.Value).Distinct(), ct);
        return new PagedResult<AdminInstallationDto>(page.Select(i => new AdminInstallationDto(
            i.Id, i.InstallationId, i.UserId, i.UserId is { } u ? names.GetValueOrDefault(u) : null, i.Platform, i.Version, i.BuildNumber, i.OsVersion,
            Status(releases, i.Platform, i.Version), i.FirstSeenAt, i.LastSeenAt)).ToList(), total, q.Skip);
    }

    public async Task<ReleaseDto> Handle(UpdateReleaseCommand c, CancellationToken ct)
    {
        var platform = VersionRules.Platform(c.Platform);
        var latest = VersionRules.OptionalVersion(c.LatestVersion, "Dernière version");
        var minimum = VersionRules.OptionalVersion(c.MinimumSupportedVersion, "Version minimale");
        var me = await admin.GetAsync(ct);
        var now = clock.GetUtcNow();
        var release = await db.Releases.FirstOrDefaultAsync(r => r.AppCode == settings.AppCode && r.Platform == platform, ct);
        if (release is null)
        {
            release = AppRelease.Create(settings.AppCode, platform, c.StoreUrl, now);
            db.Releases.Add(release);
        }

        release.Update(latest, minimum, c.StoreUrl, me.DisplayName, now);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Versions de l'application modifiées", "AppRelease", release.Id,
            $"{platform} : dernière {latest ?? "—"}, minimum {minimum ?? "—"}", ct);
        return new ReleaseDto(platform, release.LatestVersion, release.MinimumSupportedVersion, release.StoreUrl, release.UpdatedAt, release.UpdatedBy);
    }

    public async Task<PagedResult<CampaignDto>> Handle(GetCampaignsQuery q, CancellationToken ct)
    {
        var query = db.Campaigns.AsNoTracking().Where(c => c.AppCode == settings.AppCode && c.Status != CampaignStatus.Superseded);
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(c => c.CreatedAt).Skip(Math.Max(0, q.Skip)).Take(Math.Clamp(q.Take, 1, 100)).ToListAsync(ct);
        return new PagedResult<CampaignDto>(page.Select(ToDto).ToList(), total, q.Skip);
    }

    public async Task<CampaignDetailDto> Handle(GetCampaignQuery q, CancellationToken ct)
    {
        var campaign = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == q.Id, ct) ?? throw new NotFoundException("Relance");
        var recipients = await db.Recipients.AsNoTracking().Where(r => r.CampaignId == q.Id)
            .OrderByDescending(r => r.Required).ThenBy(r => r.Platform).Take(200).ToListAsync(ct);
        var names = await identity.GetNamesAsync(recipients.Select(r => r.UserId), ct);
        return new CampaignDetailDto(ToDto(campaign), recipients.Select(r => new RecipientDto(
            r.UserId, names.GetValueOrDefault(r.UserId) ?? "Membre supprimé", r.Platform, r.CurrentVersion, r.TargetVersion, r.Required, r.Status, r.NotifiedAt)).ToList());
    }

    public async Task<CampaignDto?> Handle(DetectOutdatedCommand c, CancellationToken ct)
    {
        await admin.GetAsync(ct);
        var (campaign, _) = await detector.RunAsync(ct);
        await admin.LogAsync("Détection des applications non à jour", "UpdateCampaign", campaign?.Id,
            campaign is null ? "Aucun membre à relancer" : $"{campaign.RecipientCount} membre(s) à relancer", ct);
        return campaign is null ? null : ToDto(campaign);
    }

    public async Task<CampaignDto> Handle(ApproveCampaignCommand c, CancellationToken ct)
    {
        var me = await admin.GetAsync(ct);
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Relance");
        campaign.Approve(c.SendPush, c.SendEmail, me.DisplayName, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        var channels = string.Join(" + ", new[] { c.SendPush ? "push" : null, c.SendEmail ? "email" : null }.OfType<string>());
        await admin.LogAsync("Relance de mise à jour validée", "UpdateCampaign", campaign.Id, $"{campaign.RecipientCount} membre(s), {channels}", ct);
        return ToDto(campaign);
    }

    public async Task<CampaignDto> Handle(RejectCampaignCommand c, CancellationToken ct)
    {
        var me = await admin.GetAsync(ct);
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Relance");
        campaign.Reject(c.Reason, me.DisplayName, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Relance de mise à jour refusée", "UpdateCampaign", campaign.Id, c.Reason ?? "", ct);
        return ToDto(campaign);
    }

    private static UpdateStatus Status(IReadOnlyDictionary<string, ReleaseInfo> releases, string platform, string version) =>
        releases.TryGetValue(platform, out var r) ? r.StatusOf(version) : UpdateStatus.Unknown;

    private static StatusCountsDto Counts(IEnumerable<UpdateStatus> statuses)
    {
        var list = statuses.ToList();
        return new StatusCountsDto(list.Count(s => s == UpdateStatus.UpToDate), list.Count(s => s == UpdateStatus.UpdateAvailable),
            list.Count(s => s == UpdateStatus.UpdateRequired), list.Count(s => s == UpdateStatus.Unknown));
    }

    private static ReleaseDto ToDto(ReleaseInfo r) => new(r.Platform, r.LatestVersion, r.MinimumSupportedVersion, r.StoreUrl, r.UpdatedAt, r.UpdatedBy);

    internal static CampaignDto ToDto(UpdateCampaign c) => new(c.Id, c.Status, c.CreatedAt, c.RecipientCount, c.RequiredCount, c.SendPush, c.SendEmail,
        c.DecidedBy, c.DecidedAt, c.RejectReason, c.SentAt, c.SentCount, c.SkippedCount, c.FailedCount);
}
