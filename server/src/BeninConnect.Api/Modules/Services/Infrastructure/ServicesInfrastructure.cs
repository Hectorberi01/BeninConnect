using BeninConnect.Modules.Services.Application;
using BeninConnect.Modules.Services.Contracts;
using BeninConnect.Modules.Services.Domain;
using BeninConnect.Modules.Services.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Services.Infrastructure;

public sealed class ServicesModuleService(ServicesDbContext db) : IServicesModule, IModuleStatistics
{
    public async Task<int> CountAwaitingReviewAsync(CancellationToken ct) =>
        await db.Providers.CountAsync(p => p.Status == ProviderStatus.PendingReview, ct)
        + await db.Offerings.CountAsync(o => o.HiddenReason == HiddenReason.AlertKeyword || o.HiddenReason == HiddenReason.Reports, ct)
        + await db.Reports.CountAsync(r => !r.Handled, ct)
        + await db.Documents.CountAsync(d => d.Status == DocumentStatus.Pending, ct);

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) =>
    [
        new("Prestataires", await db.Providers.CountAsync(ct)),
        new("Offres de services", await db.Offerings.CountAsync(ct)),
        new("Demandes de services", await db.Requests.CountAsync(ct)),
        new("Avis de services", await db.Reviews.CountAsync(ct))
    ];
}

/// <summary>Rappels d'avis aux demandeurs dont la prestation est terminée.</summary>
public sealed class ReviewRemindersTask(ReviewReminders reminders) : IScheduledTask
{
    public bool Enabled => true;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct) =>
        $"{await reminders.SendAsync(ct)} rappel(s) d'avis envoyé(s).";
}
