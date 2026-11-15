using BeninConnect.Modules.AppVersionManagement.Application;
using BeninConnect.Modules.AppVersionManagement.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.AppVersionManagement.Infrastructure;

public sealed class AppVersionsModuleService(AppVersionsDbContext db) : IModuleStatistics
{
    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) =>
    [
        new("Installations de l'application", await db.Installations.CountAsync(ct)),
        new("Relances de mise à jour", await db.Campaigns.CountAsync(ct))
    ];
}

/// <summary>Détection quotidienne des membres à relancer : prépare une relance à valider dans la console.</summary>
public sealed class DetectOutdatedTask(OutdatedDetector detector) : IScheduledTask
{
    public bool Enabled => true;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var (campaign, forgotten) = await detector.RunAsync(ct);
        var result = campaign is null
            ? "Aucun membre à relancer."
            : $"{campaign.RecipientCount} membre(s) à relancer ({campaign.RequiredCount} mise(s) à jour obligatoire(s)), en attente de validation.";
        return forgotten > 0 ? $"{result} {forgotten} installation(s) inactive(s) supprimée(s)." : result;
    }
}

/// <summary>Envoi des relances validées par un administrateur, par lots.</summary>
public sealed class SendCampaignsTask(CampaignSender sender) : IScheduledTask
{
    public bool Enabled => true;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var (sent, skipped, failed, rounds) = (0, 0, 0, 0);
        // Plusieurs lots par exécution, sans dépasser quelques minutes
        while (rounds++ < 20 && await sender.SendNextBatchAsync(ct) is { } batch)
        {
            (sent, skipped, failed) = (sent + batch.Sent, skipped + batch.Skipped, failed + batch.Failed);
        }

        return rounds == 1 ? "Aucune relance validée à envoyer." : $"{sent} relancé(s), {skipped} déjà à jour, {failed} en échec.";
    }
}
