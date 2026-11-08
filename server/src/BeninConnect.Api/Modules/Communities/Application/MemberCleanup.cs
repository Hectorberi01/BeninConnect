using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Communities.Application;

/// <summary>Compte supprimé : ses communautés sont supprimées, ses adhésions et demandes retirées.</summary>
public sealed class DeleteMemberCommunities(ICommunitiesDb db, IEventBus bus) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct)
    {
        var id = e.UserId;
        var owned = await db.Communities.Where(c => c.OwnerId == id).ToListAsync(ct);
        db.Communities.RemoveRange(owned);

        var joined = await db.Communities.Include(c => c.Members).Include(c => c.JoinRequests)
            .Where(c => c.OwnerId != id && (c.Members.Any(m => m.UserId == id) || c.JoinRequests.Any(r => r.UserId == id)))
            .ToListAsync(ct);
        foreach (var community in joined)
        {
            community.Leave(id);
            community.CancelJoinRequest(id);
        }

        await db.SaveChangesAsync(ct);
        foreach (var community in owned)
        {
            await bus.PublishAsync(new CommunityDeleted(community.Id), ct);
        }

        var covers = owned.Select(c => c.CoverImage).OfType<string>().ToList();
        if (covers.Count > 0)
        {
            await bus.PublishAsync(new ImagesReleased(covers), ct);
        }
    }
}
