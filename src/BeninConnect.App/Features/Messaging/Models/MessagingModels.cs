using BeninConnect.App.Shared.Models;

namespace BeninConnect.App.Features.Messaging.Models;

public sealed record Conversation(Guid Id, IReadOnlyList<Guid> ParticipantIds, DateTimeOffset CreatedAt);

public sealed record Message
{
    public required Guid Id { get; init; }
    public required Guid ConversationId { get; init; }
    public required Guid SenderId { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset SentAt { get; init; }
    public bool IsRead { get; init; }

    /// <summary>Message produit par le scénario de démonstration explicite (jamais présenté comme réel).</summary>
    public bool IsSimulated { get; init; }
}

/// <summary>Vue d'une conversation pour un utilisateur donné (forme proche d'un DTO d'API).</summary>
public sealed record ConversationSummary(
    Conversation Conversation,
    User OtherParticipant,
    Message? LastMessage,
    int UnreadCount);

public sealed record SendMessageRequest(Guid ConversationId, Guid SenderId, string Content, bool IsSimulated = false);

public sealed record ConversationChangedMessage(Guid ConversationId);
