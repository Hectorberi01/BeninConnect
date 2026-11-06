using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Messaging.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Application;

public interface IMessagingDb
{
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record ConversationDto(Guid Id, IReadOnlyList<Guid> ParticipantIds, DateTimeOffset CreatedAt);

public sealed record MessageDto(
    Guid Id, Guid ConversationId, Guid SenderId, string Content, DateTimeOffset SentAt, bool IsRead, bool IsSimulated,
    IReadOnlyList<ReactionDto> Reactions);

public sealed record ConversationSummaryDto(ConversationDto Conversation, UserDto OtherParticipant, MessageDto? LastMessage, int UnreadCount);

public static class MessagingMapping
{
    public static ConversationDto ToDto(this Conversation c) =>
        new(c.Id, c.Participants.Select(p => p.UserId).ToList(), c.CreatedAt);

    /// <summary>Nécessite l'inclusion de Reactions.</summary>
    public static MessageDto ToDto(this Message m) =>
        new(m.Id, m.ConversationId, m.SenderId, m.Content, m.SentAt, m.IsRead, m.IsSimulated, m.Reactions.ToDto());
}

public sealed record GetConversationsQuery : IQuery<IReadOnlyList<ConversationSummaryDto>>;

public sealed record GetConversationQuery(Guid Id) : IQuery<ConversationSummaryDto>;

public sealed record GetMessagesQuery(Guid ConversationId) : IQuery<IReadOnlyList<MessageDto>>;

public sealed record SendMessageCommand(Guid ConversationId, string Content) : ICommand<MessageDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => Rules.Length(nameof(Content), Content, "Le message", 1, Message.MaxLength);
}

/// <summary>Scénario de démonstration : réponse de l'autre participant, marquée comme simulée (environnement Development uniquement).</summary>
public sealed record ReactToMessageCommand(Guid ConversationId, Guid MessageId, string Emoji) : ICommand<MessageDto>;

public sealed record SimulateReplyCommand(Guid ConversationId, string Content) : ICommand<MessageDto>;

public sealed record MarkConversationReadCommand(Guid ConversationId) : ICommand<int>;

public sealed record StartDirectConversationCommand(Guid OtherUserId) : ICommand<ConversationDto>;

public sealed record GetUnreadMessageCountQuery : IQuery<CountDto>;

public sealed class ConversationHandlers(IMessagingDb db, IIdentityModule identity, IEventBus bus, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<GetConversationsQuery, IReadOnlyList<ConversationSummaryDto>>,
    IRequestHandler<GetConversationQuery, ConversationSummaryDto>,
    IRequestHandler<GetMessagesQuery, IReadOnlyList<MessageDto>>,
    IRequestHandler<SendMessageCommand, MessageDto>,
    IRequestHandler<SimulateReplyCommand, MessageDto>,
    IRequestHandler<MarkConversationReadCommand, int>,
    IRequestHandler<StartDirectConversationCommand, ConversationDto>,
    IRequestHandler<GetUnreadMessageCountQuery, CountDto>,
    IRequestHandler<ReactToMessageCommand, MessageDto>
{
    public async Task<IReadOnlyList<ConversationSummaryDto>> Handle(GetConversationsQuery request, CancellationToken ct)
    {
        var conversations = await db.Conversations.AsNoTracking().Include(c => c.Participants)
            .Where(c => c.Participants.Any(p => p.UserId == me.Id))
            .ToListAsync(ct);

        var summaries = await BuildSummariesAsync(conversations, ct);
        return summaries.OrderByDescending(s => s.LastMessage?.SentAt ?? s.Conversation.CreatedAt).ToList();
    }

    public async Task<ConversationSummaryDto> Handle(GetConversationQuery request, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(request.Id, ct);
        return (await BuildSummariesAsync([conversation], ct)).Single();
    }

    public async Task<IReadOnlyList<MessageDto>> Handle(GetMessagesQuery request, CancellationToken ct)
    {
        await LoadMineAsync(request.ConversationId, ct);
        return (await db.Messages.AsNoTracking().Include(m => m.Reactions).Where(m => m.ConversationId == request.ConversationId)
                .OrderBy(m => m.SentAt).ToListAsync(ct))
            .Select(m => m.ToDto()).ToList();
    }

    public async Task<MessageDto> Handle(SendMessageCommand request, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(request.ConversationId, ct);
        var message = conversation.Post(Guid.NewGuid(), me.Id, request.Content, clock.GetUtcNow());
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);

        var recipients = conversation.Participants.Select(p => p.UserId).Where(id => id != me.Id).ToList();
        await bus.PublishAsync(new MessageSent(conversation.Id, me.Id, recipients, Text.Excerpt(message.Content)), ct);
        return message.ToDto();
    }

    public async Task<MessageDto> Handle(SimulateReplyCommand request, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(request.ConversationId, ct);
        var other = conversation.Participants.First(p => p.UserId != me.Id).UserId;
        var message = conversation.Post(Guid.NewGuid(), other, request.Content, clock.GetUtcNow(), isSimulated: true);
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        return message.ToDto();
    }

    public async Task<MessageDto> Handle(ReactToMessageCommand request, CancellationToken ct)
    {
        await LoadMineAsync(request.ConversationId, ct);
        var message = await db.Messages.Include(m => m.Reactions)
                          .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.ConversationId == request.ConversationId, ct)
                      ?? throw new NotFoundException("Message");

        message.React(me.Id, request.Emoji, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return message.ToDto();
    }

    public async Task<int> Handle(MarkConversationReadCommand request, CancellationToken ct)
    {
        await LoadMineAsync(request.ConversationId, ct);
        var unread = await db.Messages
            .Where(m => m.ConversationId == request.ConversationId && m.SenderId != me.Id && !m.IsRead)
            .ToListAsync(ct);

        foreach (var message in unread)
        {
            message.MarkRead();
        }

        await db.SaveChangesAsync(ct);
        return unread.Count;
    }

    public async Task<ConversationDto> Handle(StartDirectConversationCommand request, CancellationToken ct)
    {
        if (!await identity.ExistsAsync(request.OtherUserId, ct))
        {
            throw new NotFoundException("Membre");
        }

        var existing = await db.Conversations.AsNoTracking().Include(c => c.Participants)
            .Where(c => c.Participants.Count == 2
                        && c.Participants.Any(p => p.UserId == me.Id)
                        && c.Participants.Any(p => p.UserId == request.OtherUserId))
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            return existing.ToDto();
        }

        var conversation = Conversation.CreateDirect(Guid.NewGuid(), me.Id, request.OtherUserId, clock.GetUtcNow());
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(ct);
        return conversation.ToDto();
    }

    public async Task<CountDto> Handle(GetUnreadMessageCountQuery request, CancellationToken ct)
    {
        var mine = db.Conversations.Where(c => c.Participants.Any(p => p.UserId == me.Id)).Select(c => c.Id);
        return new CountDto(await db.Messages.CountAsync(m => mine.Contains(m.ConversationId) && m.SenderId != me.Id && !m.IsRead, ct));
    }

    private async Task<Conversation> LoadMineAsync(Guid id, CancellationToken ct)
    {
        var conversation = await db.Conversations.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == id, ct)
                           ?? throw new NotFoundException("Conversation");
        return conversation.Includes(me.Id) ? conversation : throw new NotFoundException("Conversation");
    }

    private async Task<List<ConversationSummaryDto>> BuildSummariesAsync(IReadOnlyList<Conversation> conversations, CancellationToken ct)
    {
        var ids = conversations.Select(c => c.Id).ToList();
        var otherIds = conversations.SelectMany(c => c.Participants).Select(p => p.UserId).Where(id => id != me.Id).Distinct().ToList();
        var users = await identity.GetUsersAsync(otherIds, ct);

        var messages = await db.Messages.AsNoTracking().Where(m => ids.Contains(m.ConversationId)).ToListAsync(ct);
        var byConversation = messages.GroupBy(m => m.ConversationId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ConversationSummaryDto>();
        foreach (var c in conversations)
        {
            var otherId = c.Participants.Select(p => p.UserId).FirstOrDefault(id => id != me.Id);
            if (!users.TryGetValue(otherId, out var other))
            {
                continue;
            }

            var list = byConversation.GetValueOrDefault(c.Id) ?? [];
            result.Add(new ConversationSummaryDto(
                c.ToDto(),
                other,
                list.MaxBy(m => m.SentAt)?.ToDto(),
                list.Count(m => m.SenderId != me.Id && !m.IsRead)));
        }

        return result;
    }
}
