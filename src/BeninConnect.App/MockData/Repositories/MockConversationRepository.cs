using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Messaging.Contracts;
using BeninConnect.App.Features.Messaging.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.MockData.Repositories;

public sealed class MockConversationRepository(MockDataStore store, IMessenger messenger, DeveloperOptions developer, TimeProvider clock)
    : MockRepositoryBase(store, messenger, developer, clock), IConversationRepository
{
    public const int MaxLength = 2000;

    public async Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Conversations
                .Where(c => c.ParticipantIds.Contains(userId))
                .Select(c => BuildSummary(c, userId))
                .OfType<ConversationSummary>()
                .OrderByDescending(s => s.LastMessage?.SentAt ?? s.Conversation.CreatedAt)
                .ToList();
        }
    }

    public async Task<ConversationSummary?> GetSummaryAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            var conversation = Store.Conversations.FirstOrDefault(c => c.Id == conversationId);
            return conversation is null ? null : BuildSummary(conversation, userId);
        }
    }

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Messages.Where(m => m.ConversationId == conversationId).OrderBy(m => m.SentAt).ToList();
        }
    }

    public async Task<Message> SendAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);

        var content = request.Content.Trim();
        if (content.Length == 0)
        {
            throw new DomainException("Le message est vide.");
        }

        if (content.Length > MaxLength)
        {
            throw new DomainException($"Le message ne doit pas dépasser {MaxLength} caractères.");
        }

        var message = new Message
        {
            Id = NewId(),
            ConversationId = request.ConversationId,
            SenderId = request.SenderId,
            Content = content,
            SentAt = Now,
            IsRead = false,
            IsSimulated = request.IsSimulated
        };

        lock (Store.SyncRoot)
        {
            if (!Store.Conversations.Any(c => c.Id == request.ConversationId))
            {
                throw new NotFoundException("Conversation");
            }

            Store.Messages.Add(message);
        }

        Messenger.Send(new ConversationChangedMessage(request.ConversationId));
        return message;
    }

    public async Task MarkAsReadAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        var changed = false;
        lock (Store.SyncRoot)
        {
            for (var i = 0; i < Store.Messages.Count; i++)
            {
                var m = Store.Messages[i];
                if (m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
                {
                    Store.Messages[i] = m with { IsRead = true };
                    changed = true;
                }
            }
        }

        if (changed)
        {
            Messenger.Send(new ConversationChangedMessage(conversationId));
        }
    }

    public async Task<Conversation> GetOrCreateDirectAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        if (userId == otherUserId)
        {
            throw new DomainException("Impossible de démarrer une conversation avec soi-même.");
        }

        Conversation? conversation;
        lock (Store.SyncRoot)
        {
            conversation = Store.Conversations.FirstOrDefault(c =>
                c.ParticipantIds.Count == 2 && c.ParticipantIds.Contains(userId) && c.ParticipantIds.Contains(otherUserId));

            if (conversation is not null)
            {
                return conversation;
            }

            conversation = new Conversation(NewId(), [userId, otherUserId], Now);
            Store.Conversations.Add(conversation);
        }

        Messenger.Send(new ConversationChangedMessage(conversation.Id));
        return conversation;
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            var mine = Store.Conversations.Where(c => c.ParticipantIds.Contains(userId)).Select(c => c.Id).ToHashSet();
            return Store.Messages.Count(m => mine.Contains(m.ConversationId) && m.SenderId != userId && !m.IsRead);
        }
    }

    // Appelé sous verrou.
    private ConversationSummary? BuildSummary(Conversation conversation, Guid userId)
    {
        var otherId = conversation.ParticipantIds.FirstOrDefault(id => id != userId);
        var other = Store.Users.FirstOrDefault(u => u.Id == otherId);
        if (other is null)
        {
            return null;
        }

        var messages = Store.Messages.Where(m => m.ConversationId == conversation.Id).ToList();
        var last = messages.MaxBy(m => m.SentAt);
        var unread = messages.Count(m => m.SenderId != userId && !m.IsRead);
        return new ConversationSummary(conversation, other, last, unread);
    }
}
