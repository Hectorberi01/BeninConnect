using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Messaging.Domain;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Messaging.Application;

// Support : discussion de chaque membre avec le compte officiel, traitée par l'équipe depuis la console.

/// <summary>Ouvre (ou crée) la discussion du membre avec le compte officiel.</summary>
public sealed record OpenSupportConversationCommand : ICommand<ConversationDto>;

/// <summary>UnreadCount : messages du membre pas encore lus par l'équipe.</summary>
public sealed record SupportConversationDto(Guid Id, UserDto Member, MessageDto? LastMessage, int UnreadCount, DateTimeOffset UpdatedAt);

public sealed record SupportThreadDto(Guid Id, UserDto Member, Guid OfficialId, IReadOnlyList<MessageDto> Messages);

public sealed record GetSupportConversationsQuery(bool UnreadOnly) : IQuery<IReadOnlyList<SupportConversationDto>>;

public sealed record GetSupportThreadQuery(Guid ConversationId) : IQuery<SupportThreadDto>;

public sealed record GetSupportUnreadCountQuery : IQuery<CountDto>;

public sealed record ReplySupportCommand(Guid ConversationId, string Content) : ICommand<MessageDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => Rules.Length(nameof(Content), Content, "La réponse", 1, Message.MaxLength);
}

public sealed record MarkSupportReadCommand(Guid ConversationId) : ICommand<CountDto>;

public sealed class SupportHandlers(
    IMessagingDb db, IIdentityModule identity, IEventBus bus, IChatNotifier live, IAdminSession admin, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<OpenSupportConversationCommand, ConversationDto>,
    IRequestHandler<GetSupportConversationsQuery, IReadOnlyList<SupportConversationDto>>,
    IRequestHandler<GetSupportThreadQuery, SupportThreadDto>,
    IRequestHandler<GetSupportUnreadCountQuery, CountDto>,
    IRequestHandler<ReplySupportCommand, MessageDto>,
    IRequestHandler<MarkSupportReadCommand, CountDto>
{
    public async Task<ConversationDto> Handle(OpenSupportConversationCommand request, CancellationToken ct)
    {
        var official = await identity.GetOfficialAccountAsync(ct);
        if (official == me.Id)
        {
            throw new DomainException("Le compte officiel ne peut pas s'écrire à lui-même.");
        }

        var existing = await db.Conversations.AsNoTracking().Include(c => c.Participants)
            .Where(c => c.Participants.Count == 2 && c.Participants.Any(p => p.UserId == me.Id) && c.Participants.Any(p => p.UserId == official))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return existing.ToDto();
        }

        var conversation = Conversation.CreateDirect(Guid.NewGuid(), me.Id, official, clock.GetUtcNow());
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(ct);
        return conversation.ToDto();
    }

    public async Task<IReadOnlyList<SupportConversationDto>> Handle(GetSupportConversationsQuery request, CancellationToken ct)
    {
        var official = await identity.GetOfficialAccountAsync(ct);
        var conversations = await db.Conversations.AsNoTracking().Include(c => c.Participants)
            .Where(c => c.Participants.Any(p => p.UserId == official)).ToListAsync(ct);
        var ids = conversations.Select(c => c.Id).ToList();

        // Métadonnées seulement, puis le dernier message de chaque discussion
        var meta = await db.Messages.AsNoTracking().Where(m => ids.Contains(m.ConversationId))
            .Select(m => new { m.Id, m.ConversationId, m.SenderId, m.SentAt, m.IsRead }).ToListAsync(ct);
        var byConversation = meta.GroupBy(m => m.ConversationId).ToDictionary(g => g.Key, g => g.ToList());
        var lastIds = byConversation.Values.Select(list => list.MaxBy(m => m.SentAt)!.Id).ToList();
        var last = await db.Messages.AsNoTracking().Include(m => m.Reactions).Where(m => lastIds.Contains(m.Id)).ToDictionaryAsync(m => m.ConversationId, ct);

        var memberIds = conversations.Select(c => Member(c, official)).ToList();
        var users = await identity.GetUsersAsync(memberIds, ct);

        return conversations
            .Where(c => byConversation.ContainsKey(c.Id) && users.ContainsKey(Member(c, official)))
            .Select(c =>
            {
                var unread = byConversation[c.Id].Count(m => m.SenderId != official && !m.IsRead);
                var message = last.GetValueOrDefault(c.Id);
                return new SupportConversationDto(c.Id, users[Member(c, official)], message?.ToDto(), unread, message?.SentAt ?? c.CreatedAt);
            })
            .Where(s => !request.UnreadOnly || s.UnreadCount > 0)
            .OrderByDescending(s => s.UnreadCount > 0).ThenByDescending(s => s.UpdatedAt)
            .ToList();
    }

    public async Task<SupportThreadDto> Handle(GetSupportThreadQuery request, CancellationToken ct)
    {
        var (conversation, official) = await LoadAsync(request.ConversationId, ct);
        var memberId = Member(conversation, official);
        var member = (await identity.GetUsersAsync([memberId], ct)).GetValueOrDefault(memberId) ?? throw new NotFoundException("Membre");
        var messages = await db.Messages.AsNoTracking().Include(m => m.Reactions)
            .Where(m => m.ConversationId == conversation.Id).OrderBy(m => m.SentAt).ToListAsync(ct);
        return new SupportThreadDto(conversation.Id, member, official, await db.ToDtosAsync(messages, ct));
    }

    public async Task<CountDto> Handle(GetSupportUnreadCountQuery request, CancellationToken ct)
    {
        var official = await identity.GetOfficialAccountAsync(ct);
        var support = db.Conversations.Where(c => c.Participants.Any(p => p.UserId == official)).Select(c => c.Id);
        return new CountDto(await db.Messages.CountAsync(m => support.Contains(m.ConversationId) && m.SenderId != official && !m.IsRead, ct));
    }

    public async Task<MessageDto> Handle(ReplySupportCommand request, CancellationToken ct)
    {
        var (conversation, official) = await LoadAsync(request.ConversationId, ct);
        var memberId = Member(conversation, official);
        var message = conversation.Post(Guid.NewGuid(), official, request.Content, clock.GetUtcNow());
        db.Messages.Add(message);
        await MarkReadAsync(conversation.Id, official, ct);
        await db.SaveChangesAsync(ct);

        // Temps réel, notification et push pour le membre
        var dto = (await db.ToDtosAsync([message], ct))[0];
        await live.MessageCreatedAsync([memberId, official], dto, ct);
        await bus.PublishAsync(new MessageSent(conversation.Id, official, [memberId], Text.Excerpt(message.Preview())), ct);
        await admin.LogAsync("Réponse au support", "Member", memberId, Text.Excerpt(request.Content), ct);
        return dto;
    }

    public async Task<CountDto> Handle(MarkSupportReadCommand request, CancellationToken ct)
    {
        var (conversation, official) = await LoadAsync(request.ConversationId, ct);
        var count = await MarkReadAsync(conversation.Id, official, ct);
        await db.SaveChangesAsync(ct);
        if (count > 0)
        {
            await live.ConversationReadAsync([Member(conversation, official)], conversation.Id, official, ct);
        }

        return new CountDto(count);
    }

    private async Task<int> MarkReadAsync(Guid conversationId, Guid official, CancellationToken ct)
    {
        var unread = await db.Messages.Where(m => m.ConversationId == conversationId && m.SenderId != official && !m.IsRead).ToListAsync(ct);
        unread.ForEach(m => m.MarkRead());
        return unread.Count;
    }

    private static Guid Member(Conversation c, Guid official) => c.Participants.Select(p => p.UserId).FirstOrDefault(id => id != official);

    /// <summary>Discussions du compte officiel uniquement (404 sinon).</summary>
    private async Task<(Conversation Conversation, Guid Official)> LoadAsync(Guid id, CancellationToken ct)
    {
        var official = await identity.GetOfficialAccountAsync(ct);
        var conversation = await db.Conversations.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == id, ct);
        return conversation is not null && conversation.Includes(official) ? (conversation, official) : throw new NotFoundException("Discussion");
    }
}
