using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Messaging.Contracts;

public interface IMessagingModule
{
    Task<int> CountMessagesAsync(CancellationToken ct);

    Task<IReadOnlyList<DateTimeOffset>> GetMessageDatesAsync(DateTimeOffset since, CancellationToken ct);
}

public sealed record MessageSent(Guid ConversationId, Guid SenderId, IReadOnlyList<Guid> RecipientIds, string Excerpt) : IIntegrationEvent;
