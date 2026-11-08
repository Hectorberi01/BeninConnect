using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Events.Application;

/// <summary>Compte supprimé : ses événements sont supprimés et ses inscriptions retirées.</summary>
public sealed class DeleteMemberEvents(IEventsDb db, IEventBus bus, TimeProvider clock) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct)
    {
        var id = e.UserId;
        var organized = await db.Events.Where(x => x.OrganizerId == id).ToListAsync(ct);
        db.Events.RemoveRange(organized);

        foreach (var ev in await db.Events.Include(x => x.Participants)
                     .Where(x => x.OrganizerId != id && x.Participants.Any(p => p.UserId == id)).ToListAsync(ct))
        {
            ev.ToggleParticipation(id, clock.GetUtcNow()); // inscrit : désinscription
        }

        await db.SaveChangesAsync(ct);
        var images = organized.Select(x => x.ImagePath).OfType<string>().ToList();
        if (images.Count > 0)
        {
            await bus.PublishAsync(new ImagesReleased(images), ct);
        }
    }
}
