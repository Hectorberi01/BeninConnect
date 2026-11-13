using BeninConnect.Modules.Jobs.Contracts;
using BeninConnect.Modules.Jobs.Domain;
using BeninConnect.Modules.Jobs.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Jobs.Infrastructure;

public sealed class JobsModuleService(JobsDbContext db) : IJobsModule, IModuleStatistics
{
    public Task<int> CountAwaitingReviewAsync(CancellationToken ct) => db.Offers.CountAsync(o => o.NeedsReview, ct);

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) =>
    [
        new("Offres d'emploi", await db.Offers.CountAsync(ct)),
        new("Offres d'emploi actives", await db.Offers.CountAsync(o => o.Status == JobOfferStatus.Active, ct)),
        new("Favoris d'offres", await db.Favorites.CountAsync(ct))
    ];
}
