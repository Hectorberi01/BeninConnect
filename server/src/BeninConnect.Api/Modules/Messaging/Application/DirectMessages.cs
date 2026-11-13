using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Messaging.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Application;

/// <summary>Message envoyé au nom d'un membre par un autre module (ex. demande de devis) : conversation directe créée au besoin.</summary>
public sealed class DirectMessages(IMessagingDb db, IIdentityModule identity, IEventBus bus, IChatNotifier live, TimeProvider clock)
{
    /// <summary>notifyRecipient faux : l'appelant envoie sa propre notification (pas de doublon).</summary>
    public async Task<Guid> SendAsync(Guid senderId, Guid recipientId, string content, bool notifyRecipient, CancellationToken ct)
    {
        if ((await identity.GetBlockedRelationsAsync(senderId, ct)).Contains(recipientId))
        {
            throw new DomainException("Vous ne pouvez plus échanger de messages avec ce membre.");
        }

        var now = clock.GetUtcNow();
        var conversation = await db.Conversations.Include(c => c.Participants)
                               .Where(c => c.Participants.Count == 2
                                           && c.Participants.Any(p => p.UserId == senderId)
                                           && c.Participants.Any(p => p.UserId == recipientId))
                               .FirstOrDefaultAsync(ct);
        if (conversation is null)
        {
            conversation = Conversation.CreateDirect(Guid.NewGuid(), senderId, recipientId, now);
            db.Conversations.Add(conversation);
        }

        var message = conversation.Post(Guid.NewGuid(), senderId, content, now);
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);

        var participants = conversation.Participants.Select(p => p.UserId).ToList();
        await live.MessageCreatedAsync(participants, message.ToDto(), ct);
        if (notifyRecipient)
        {
            await bus.PublishAsync(new MessageSent(conversation.Id, senderId, [recipientId], Text.Excerpt(message.Preview())), ct);
        }

        return conversation.Id;
    }
}
