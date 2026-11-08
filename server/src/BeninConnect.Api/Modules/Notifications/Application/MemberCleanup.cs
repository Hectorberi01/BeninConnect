using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Notifications.Application;

/// <summary>Compte supprimé : notifications et téléphones enregistrés effacés.</summary>
public sealed class DeleteMemberNotifications(INotificationsDb db) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct)
    {
        await db.Notifications.Where(n => n.UserId == e.UserId).ExecuteDeleteAsync(ct);
        await db.PushDevices.Where(d => d.UserId == e.UserId).ExecuteDeleteAsync(ct);
    }
}
