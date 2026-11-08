using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Moderation.Application;

/// <summary>Compte supprimé : les signalements qu'il avait envoyés sont effacés.</summary>
public sealed class DeleteMemberReports(IModerationDb db) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct) =>
        await db.Reports.Where(r => r.ReporterId == e.UserId).ExecuteDeleteAsync(ct);
}
