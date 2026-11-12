using BeninConnect.Modules.Editorial.Contracts;
using BeninConnect.Modules.Editorial.Domain;
using BeninConnect.Modules.Editorial.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Editorial.Infrastructure;

public sealed class EditorialModuleService(EditorialDbContext db) : IEditorialModule, IModuleStatistics
{
    public Task<int> CountAwaitingReviewAsync(CancellationToken ct) => db.Candidates.CountAsync(c => c.Status == CandidateStatus.Drafted, ct);

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) =>
    [
        new("Contenus éditoriaux", await db.Candidates.CountAsync(ct)),
        new("Sources éditoriales", await db.Sources.CountAsync(ct))
    ];
}
