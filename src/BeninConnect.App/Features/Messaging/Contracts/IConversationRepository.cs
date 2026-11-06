using BeninConnect.App.Features.Messaging.Models;

namespace BeninConnect.App.Features.Messaging.Contracts;

public interface IConversationRepository
{
    Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ConversationSummary?> GetSummaryAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<Message> SendAsync(SendMessageRequest request, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);

    Task<Conversation> GetOrCreateDirectAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
