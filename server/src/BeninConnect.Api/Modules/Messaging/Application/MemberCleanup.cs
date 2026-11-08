using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Application;

/// <summary>Compte supprimé : ses conversations (messages compris) sont effacées.</summary>
public sealed class DeleteMemberConversations(IMessagingDb db, IEventBus bus) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct)
    {
        var conversations = await db.Conversations.Where(c => c.Participants.Any(p => p.UserId == e.UserId)).ToListAsync(ct);
        var ids = conversations.Select(c => c.Id).ToList();
        var files = await db.Messages.Where(m => ids.Contains(m.ConversationId) && m.Attachment != null)
            .Select(m => m.Attachment!.Name).ToListAsync(ct);
        await db.Messages.Where(m => ids.Contains(m.ConversationId)).ExecuteDeleteAsync(ct);
        db.Conversations.RemoveRange(conversations);
        await db.SaveChangesAsync(ct);
        if (files.Count > 0)
        {
            await bus.PublishAsync(new AttachmentsReleased(files), ct);
        }
    }
}
