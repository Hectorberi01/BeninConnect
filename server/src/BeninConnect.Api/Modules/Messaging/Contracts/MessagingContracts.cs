using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Messaging.Contracts;

/// <summary>Activité de messagerie d'un membre (console) : compteurs seulement, jamais le contenu.</summary>
public sealed record MemberMessagingStats(int Conversations, int MessagesSent, DateTimeOffset? LastMessageAt);

public interface IMessagingModule
{
    Task<MemberMessagingStats> GetMemberStatsAsync(Guid userId, CancellationToken ct);

    Task<int> CountMessagesAsync(CancellationToken ct);

    Task<IReadOnlyList<DateTimeOffset>> GetMessageDatesAsync(DateTimeOffset since, CancellationToken ct);

    /// <summary>Message de senderId à recipientId (conversation directe créée au besoin) ; renvoie la conversation. notifyRecipient faux : pas de notification « message ».</summary>
    Task<Guid> SendDirectAsync(Guid senderId, Guid recipientId, string content, bool notifyRecipient, CancellationToken ct);
}

public sealed record MessageSent(Guid ConversationId, Guid SenderId, IReadOnlyList<Guid> RecipientIds, string Excerpt) : IIntegrationEvent;
