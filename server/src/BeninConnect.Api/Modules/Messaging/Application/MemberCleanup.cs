using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Shared.Application;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Application;

/// <summary>Compte supprimé : ses conversations (messages compris) sont effacées.</summary>
public sealed class DeleteMemberConversations(IMessagingDb db) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct)
    {
        var conversations = await db.Conversations.Where(c => c.Participants.Any(p => p.UserId == e.UserId)).ToListAsync(ct);
        var ids = conversations.Select(c => c.Id).ToList();
        await db.Messages.Where(m => ids.Contains(m.ConversationId)).ExecuteDeleteAsync(ct);
        db.Conversations.RemoveRange(conversations);
        await db.SaveChangesAsync(ct);
    }
}
