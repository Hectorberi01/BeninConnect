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

/// <summary>Rappels d'avis : toutes les heures.</summary>
public sealed class ServicesWorker(IServiceScopeFactory scopes, ServicesOptions options, TimeProvider clock, ILogger<ServicesWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.WorkerEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<ReviewReminders>().SendAsync(stoppingToken);
                if (sent > 0)
                {
                    log.LogInformation("Services : {Count} rappel(s) d'avis envoyé(s)", sent);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Services : rappels d'avis en échec");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
