using BeninConnect.Modules.Voyages.Application;
using BeninConnect.Modules.Voyages.Contracts;
using BeninConnect.Modules.Voyages.Domain;
using BeninConnect.Modules.Voyages.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Voyages.Infrastructure;

public sealed class VoyagesModuleService(VoyagesDbContext db) : IVoyagesModule, IModuleStatistics
{
    public async Task<int> CountAwaitingReviewAsync(CancellationToken ct) =>
        await db.Bookings.CountAsync(b => b.Status == BookingStatus.Disputed, ct)
        + await db.Reports.CountAsync(r => !r.Handled, ct)
        + await db.Trips.CountAsync(t => t.HiddenReason == TripHiddenReason.Reports, ct);

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) =>
    [
        new("Trajets", await db.Trips.CountAsync(ct)),
        new("Réservations de kilos", await db.Bookings.CountAsync(ct)),
        new("Avis de voyages", await db.Reviews.CountAsync(ct)),
        new("Alertes de trajet", await db.Alerts.CountAsync(ct))
    ];
}

/// <summary>Cycle de vie des trajets : rappels avant départ, trajets partis, annonces des membres suspendus.</summary>
public sealed class TripLifecycleTask(TripLifecycle lifecycle) : IScheduledTask
{
    public bool Enabled => true;

    public async Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct) =>
        $"{await lifecycle.RunAsync(ct)} trajet(s) mis à jour.";
}
