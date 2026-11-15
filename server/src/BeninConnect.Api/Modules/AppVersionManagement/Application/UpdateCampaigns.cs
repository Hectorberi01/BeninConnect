using BeninConnect.Modules.AppVersionManagement.Domain;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Notifications.Contracts;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.AppVersionManagement.Application;

/// <summary>
/// Détection quotidienne : membres dont l'installation la plus récente (par plateforme) n'est pas à jour.
/// Prépare une relance en attente de validation (la précédente non validée est remplacée) ; rien n'est envoyé ici.
/// </summary>
public sealed class OutdatedDetector(IAppVersionsDb db, AppVersionSettings settings, TimeProvider clock)
{
    public async Task<(UpdateCampaign? Campaign, int Forgotten)> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var forgotten = await db.Installations.Where(i => i.LastSeenAt < now.AddDays(-Math.Max(30, settings.ForgetAfterDays))).ExecuteDeleteAsync(ct);

        var releases = await AppVersionsData.ReleasesAsync(db, settings, ct);
        var rows = await db.Installations.AsNoTracking()
            .Where(i => i.AppCode == settings.AppCode && i.UserId != null && i.LastSeenAt >= now.AddDays(-settings.ActiveDays))
            .Select(i => new { UserId = i.UserId!.Value, i.Platform, i.Version, i.LastSeenAt })
            .ToListAsync(ct);

        // Relancés récemment, ou dans une relance en cours d'envoi
        var recent = now.AddDays(-Math.Max(1, settings.NotifyEveryDays));
        var excluded = (await db.Recipients.AsNoTracking()
            .Where(r => (r.Status == RecipientStatus.Sent && r.NotifiedAt >= recent)
                        || (r.Status == RecipientStatus.Pending && db.Campaigns.Any(c => c.Id == r.CampaignId && c.Status == CampaignStatus.Approved)))
            .Select(r => r.UserId).ToListAsync(ct)).ToHashSet();

        var recipients = AppVersionsData.LatestPerUserAndPlatform(rows, r => r.UserId, r => r.Platform, r => r.LastSeenAt)
            .Select(r => (Row: r, Release: releases.GetValueOrDefault(r.Platform)))
            .Where(x => x.Release is not null && !excluded.Contains(x.Row.UserId))
            .Select(x => (x.Row, x.Release, Status: x.Release!.StatusOf(x.Row.Version)))
            .Where(x => x.Status.IsOutdated() && x.Release!.Target is not null)
            // Un membre, une relance : la plus urgente de ses plateformes
            .GroupBy(x => x.Row.UserId)
            .Select(g => g.OrderByDescending(x => x.Status).First())
            .Select(x => CampaignRecipient.For(x.Row.UserId, x.Row.Platform, x.Row.Version, x.Release!.Target!, x.Status == UpdateStatus.UpdateRequired))
            .ToList();

        foreach (var pending in await db.Campaigns.Where(c => c.AppCode == settings.AppCode && c.Status == CampaignStatus.PendingApproval).ToListAsync(ct))
        {
            pending.Supersede();
        }

        UpdateCampaign? campaign = null;
        if (recipients.Count > 0)
        {
            campaign = UpdateCampaign.Prepare(settings.AppCode, recipients, now);
            db.Campaigns.Add(campaign);
        }

        await db.SaveChangesAsync(ct);
        return (campaign, forgotten);
    }
}

/// <summary>
/// Envoi des relances validées, par lots : notification (in-app + push) et/ou email. Un membre passé à jour
/// entre la détection et l'envoi n'est pas relancé.
/// </summary>
public sealed class CampaignSender(IAppVersionsDb db, AppVersionSettings settings, IEventBus bus, IIdentityModule identity, TimeProvider clock)
{
    public async Task<(int Sent, int Skipped, int Failed, bool Finished)?> SendNextBatchAsync(CancellationToken ct)
    {
        var campaign = await db.Campaigns.Where(c => c.Status == CampaignStatus.Approved).OrderBy(c => c.DecidedAt).FirstOrDefaultAsync(ct);
        if (campaign is null)
        {
            return null;
        }

        var batch = await db.Recipients.Where(r => r.CampaignId == campaign.Id && r.Status == RecipientStatus.Pending)
            .OrderBy(r => r.Id).Take(Math.Max(1, settings.SendBatchSize)).ToListAsync(ct);
        var releases = await AppVersionsData.ReleasesAsync(db, settings, ct);
        var users = batch.Select(r => r.UserId).ToList();
        var rows = await db.Installations.AsNoTracking()
            .Where(i => i.AppCode == campaign.AppCode && i.UserId != null && users.Contains(i.UserId!.Value))
            .Select(i => new { UserId = i.UserId!.Value, i.Platform, i.Version, i.LastSeenAt }).ToListAsync(ct);
        var current = AppVersionsData.LatestPerUserAndPlatform(rows, r => r.UserId, r => r.Platform, r => r.LastSeenAt)
            .ToDictionary(r => (r.UserId, r.Platform), r => r.Version);

        var now = clock.GetUtcNow();
        var (sent, skipped, failed) = (0, 0, 0);
        foreach (var r in batch)
        {
            var release = releases[r.Platform];
            var version = current.GetValueOrDefault((r.UserId, r.Platform));
            if (version is null || !release.StatusOf(version).IsOutdated())
            {
                r.Mark(RecipientStatus.Skipped, now);
                skipped++;
                continue;
            }

            var required = release.StatusOf(version) == UpdateStatus.UpdateRequired;
            var target = release.Target!;
            var delivered = false;
            if (campaign.SendPush)
            {
                await bus.PublishAsync(new NotificationRequested(r.UserId, "System", Title(required), PushBody(version, target, required), "None", null), ct);
                delivered = true;
            }

            if (campaign.SendEmail)
            {
                try
                {
                    delivered |= await identity.SendEmailAsync(r.UserId, Subject(required), EmailBody(version, target, required, release.StoreUrl), ct);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // Le push est parti : l'email en échec ne bloque pas le lot
                }
            }

            r.Mark(delivered ? RecipientStatus.Sent : RecipientStatus.Failed, now);
            if (delivered)
            {
                sent++;
            }
            else
            {
                failed++;
            }
        }

        var finished = batch.Count < Math.Max(1, settings.SendBatchSize)
                       || !await db.Recipients.AnyAsync(x => x.CampaignId == campaign.Id && x.Status == RecipientStatus.Pending && !batch.Select(b => b.Id).Contains(x.Id), ct);
        campaign.Progress(sent, skipped, failed, finished, now);
        await db.SaveChangesAsync(ct);
        return (sent, skipped, failed, finished);
    }

    private string Title(bool required) => required ? "Mise à jour obligatoire" : "Nouvelle version disponible";

    private string Subject(bool required) =>
        required ? $"Mise à jour obligatoire de {settings.AppName}" : $"Une nouvelle version de {settings.AppName} est disponible";

    private string PushBody(string version, string target, bool required) => required
        ? $"Votre version {version} n'est plus prise en charge : installez la version {target} pour continuer à utiliser {settings.AppName}."
        : $"{settings.AppName} {target} est disponible : mettez à jour pour profiter des dernières améliorations.";

    private string EmailBody(string version, string target, bool required, string storeUrl) =>
        $"""
         Bonjour,

         {(required
             ? $"La version {version} de {settings.AppName} installée sur votre téléphone n'est plus prise en charge. Installez la version {target} pour continuer à utiliser l'application."
             : $"La version {target} de {settings.AppName} est disponible. Vous utilisez encore la version {version} : mettez à jour pour profiter des dernières améliorations et corrections.")}

         Mettre à jour : {storeUrl}

         L'équipe {settings.AppName}
         """;
}
