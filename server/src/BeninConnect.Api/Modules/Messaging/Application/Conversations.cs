using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Messaging.Contracts;
using BeninConnect.Modules.Media.Contracts;
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

public sealed record AttachmentDto(AttachmentKind Kind, string FileName, string ContentType, long Size, int? DurationMs);

public sealed record MessageDto(
    Guid Id, Guid ConversationId, Guid SenderId, string Content, DateTimeOffset SentAt, bool IsRead, bool IsSimulated,
    IReadOnlyList<ReactionDto> Reactions, DateTimeOffset? EditedAt = null, bool IsDeleted = false, AttachmentDto? Attachment = null,
    Guid? SharedPostId = null);

/// <summary>Fichier d'une pièce jointe, après contrôle d'accès.</summary>
public sealed record AttachmentDownload(Stream Content, long? Length, string ContentType, string FileName);

/// <summary>Diffusion en temps réel aux participants connectés (SignalR) ; sans effet pour les membres hors ligne.</summary>
public interface IChatNotifier
{
    Task MessageCreatedAsync(IReadOnlyCollection<Guid> userIds, MessageDto message, CancellationToken ct);

    /// <summary>Modification, suppression ou réaction.</summary>
    Task MessageUpdatedAsync(IReadOnlyCollection<Guid> userIds, MessageDto message, CancellationToken ct);

    Task ConversationReadAsync(IReadOnlyCollection<Guid> userIds, Guid conversationId, Guid readerId, CancellationToken ct);
}

/// <summary>IsBlocked : l'un des deux a bloqué l'autre. OtherOnline / OtherLastSeenAt : présence de l'autre participant.</summary>
public sealed record ConversationSummaryDto(
    ConversationDto Conversation, UserDto OtherParticipant, MessageDto? LastMessage, int UnreadCount, bool IsBlocked = false,
    bool OtherOnline = false, DateTimeOffset? OtherLastSeenAt = null);

/// <summary>Membres connectés au temps réel (une ou plusieurs connexions) et dernière déconnexion.</summary>
public interface IPresence
{
    bool IsOnline(Guid userId);

    DateTimeOffset? LastSeen(Guid userId);
}

public static class MessagingMapping
{
    public static ConversationDto ToDto(this Conversation c) =>
        new(c.Id, c.Participants.Select(p => p.UserId).ToList(), c.CreatedAt);

    /// <summary>Nécessite l'inclusion de Reactions.</summary>
    public static MessageDto ToDto(this Message m) =>
        new(m.Id, m.ConversationId, m.SenderId, m.Content, m.SentAt, m.IsRead, m.IsSimulated, m.Reactions.ToDto(), m.EditedAt, m.IsDeleted,
            m.Attachment is { } a ? new AttachmentDto(a.Kind, a.FileName, a.ContentType, a.Size, a.DurationMs) : null, m.SharedPostId);

    /// <summary>Aperçu (notification, liste des conversations) : texte ou type de pièce jointe.</summary>
    public static string Preview(this Message m) => m.Attachment switch
    {
        { Kind: AttachmentKind.Image } => string.IsNullOrEmpty(m.Content) ? "📷 Photo" : $"📷 {m.Content}",
        { Kind: AttachmentKind.Audio } => "🎤 Message vocal",
        { } file => $"📄 {file.FileName}",
        null when m.SharedPostId is not null => string.IsNullOrEmpty(m.Content) ? "📰 Publication partagée" : $"📰 {m.Content}",
        null => m.Content,
    };
}

public sealed record GetConversationsQuery : IQuery<IReadOnlyList<ConversationSummaryDto>>;

public sealed record GetConversationQuery(Guid Id) : IQuery<ConversationSummaryDto>;

public sealed record GetMessagesQuery(Guid ConversationId) : IQuery<IReadOnlyList<MessageDto>>;

/// <summary>PostId : publication partagée (texte facultatif).</summary>
public sealed record SendMessageCommand(Guid ConversationId, string Content, Guid? PostId = null) : ICommand<MessageDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => PostId is not null && string.IsNullOrWhiteSpace(Content)
        ? []
        : Rules.Length(nameof(Content), Content, "Le message", 1, Message.MaxLength);
}

/// <summary>Scénario de démonstration : réponse de l'autre participant, marquée comme simulée (environnement Development uniquement).</summary>
public sealed record ReactToMessageCommand(Guid ConversationId, Guid MessageId, string Emoji) : ICommand<MessageDto>;

public sealed record SimulateReplyCommand(Guid ConversationId, string Content) : ICommand<MessageDto>;

public sealed record MarkConversationReadCommand(Guid ConversationId) : ICommand<int>;

public sealed record EditMessageCommand(Guid ConversationId, Guid MessageId, string Content) : ICommand<MessageDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => Rules.Length(nameof(Content), Content, "Le message", 1, Message.MaxLength);
}

public sealed record DeleteMessageCommand(Guid ConversationId, Guid MessageId) : ICommand<MessageDto>;

/// <summary>Photo, PDF ou message vocal (20 Mo), avec une légende facultative. Content doit permettre la lecture répétée.</summary>
public sealed record SendAttachmentCommand(Guid ConversationId, Stream Content, long Length, string? FileName, string? Caption, int? DurationMs)
    : ICommand<MessageDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate()
    {
        if ((Caption?.Length ?? 0) > Message.MaxLength)
        {
            yield return (nameof(Caption), $"Le message ne doit pas dépasser {Message.MaxLength} caractères.");
        }
    }
}

/// <summary>Téléchargement réservé aux participants de la conversation.</summary>
public sealed record GetAttachmentQuery(Guid MessageId) : IQuery<AttachmentDownload>;

public sealed record StartDirectConversationCommand(Guid OtherUserId) : ICommand<ConversationDto>;

public sealed record GetUnreadMessageCountQuery : IQuery<CountDto>;

/// <summary>Retire la discussion de ma liste (pour moi seulement) ; elle revient si un nouveau message arrive.</summary>
public sealed record DeleteConversationCommand(Guid ConversationId) : ICommand<CountDto>;

public sealed class ConversationHandlers(
    IMessagingDb db, IIdentityModule identity, IMediaModule media, IFeedModule feed, IPresence presence, IEventBus bus, IChatNotifier live,
    ICurrentUser me, TimeProvider clock) :
    IRequestHandler<GetConversationsQuery, IReadOnlyList<ConversationSummaryDto>>,
    IRequestHandler<GetConversationQuery, ConversationSummaryDto>,
    IRequestHandler<GetMessagesQuery, IReadOnlyList<MessageDto>>,
    IRequestHandler<SendMessageCommand, MessageDto>,
    IRequestHandler<SimulateReplyCommand, MessageDto>,
    IRequestHandler<MarkConversationReadCommand, int>,
    IRequestHandler<StartDirectConversationCommand, ConversationDto>,
    IRequestHandler<GetUnreadMessageCountQuery, CountDto>,
    IRequestHandler<ReactToMessageCommand, MessageDto>,
    IRequestHandler<EditMessageCommand, MessageDto>,
    IRequestHandler<DeleteMessageCommand, MessageDto>,
    IRequestHandler<SendAttachmentCommand, MessageDto>,
    IRequestHandler<GetAttachmentQuery, AttachmentDownload>,
    IRequestHandler<DeleteConversationCommand, CountDto>
{
    public async Task<IReadOnlyList<ConversationSummaryDto>> Handle(GetConversationsQuery request, CancellationToken ct)
    {
        var conversations = await db.Conversations.AsNoTracking().Include(c => c.Participants)
            .Where(c => c.Participants.Any(p => p.UserId == me.Id))
            .ToListAsync(ct);

        var summaries = await BuildSummariesAsync(conversations, ct);
        // Discussion supprimée : masquée tant qu'aucun nouveau message n'est arrivé
        return summaries
            .Where(s => s.LastMessage is not null || conversations.First(c => c.Id == s.Conversation.Id).ClearedAtFor(me.Id) is null)
            .OrderByDescending(s => s.LastMessage?.SentAt ?? s.Conversation.CreatedAt).ToList();
    }

    public async Task<ConversationSummaryDto> Handle(GetConversationQuery request, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(request.Id, ct);
        return (await BuildSummariesAsync([conversation], ct)).Single();
    }

    public async Task<IReadOnlyList<MessageDto>> Handle(GetMessagesQuery request, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(request.ConversationId, ct);
        var since = conversation.ClearedAtFor(me.Id) ?? DateTimeOffset.MinValue;
        return (await db.Messages.AsNoTracking().Include(m => m.Reactions)
                .Where(m => m.ConversationId == request.ConversationId && m.SentAt > since)
                .OrderBy(m => m.SentAt).ToListAsync(ct))
            .Select(m => m.ToDto()).ToList();
    }

    public async Task<MessageDto> Handle(SendMessageCommand request, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(request.ConversationId, ct);
        await EnsureNotBlockedAsync(conversation.Participants.Select(p => p.UserId), ct);
        if (request.PostId is { } postId && !await feed.CanSeePostAsync(postId, ct))
        {
            throw new NotFoundException("Publication");
        }

        var message = conversation.Post(Guid.NewGuid(), me.Id, request.Content, clock.GetUtcNow(), sharedPostId: request.PostId);
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);

        return await AnnounceAsync(conversation, message, ct);
    }

    public async Task<MessageDto> Handle(SendAttachmentCommand request, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(request.ConversationId, ct);
        await EnsureNotBlockedAsync(conversation.Participants.Select(p => p.UserId), ct);
        var stored = await media.SaveAttachmentAsync(request.Content, request.Length, ct);
        var kind = Enum.Parse<AttachmentKind>(stored.Kind);
        var fallback = kind switch { AttachmentKind.Audio => "Message vocal", AttachmentKind.Image => "Photo", _ => "Document" };
        var attachment = new MessageAttachment(stored.Name, kind, MessageAttachment.CleanFileName(request.FileName, fallback), stored.ContentType,
            stored.Size, kind == AttachmentKind.Audio && request.DurationMs is > 0 ? request.DurationMs : null);

        try
        {
            var message = conversation.Post(Guid.NewGuid(), me.Id, request.Caption ?? string.Empty, clock.GetUtcNow(), attachment: attachment);
            db.Messages.Add(message);
            await db.SaveChangesAsync(ct);
            return await AnnounceAsync(conversation, message, ct);
        }
        catch
        {
            await bus.PublishAsync(new AttachmentsReleased([stored.Name]), ct); // message refusé : fichier orphelin effacé
            throw;
        }
    }

    public async Task<AttachmentDownload> Handle(GetAttachmentQuery request, CancellationToken ct)
    {
        var message = await db.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MessageId, ct);
        if (message?.Attachment is not { } attachment)
        {
            throw new NotFoundException("Pièce jointe");
        }

        await LoadMineAsync(message.ConversationId, ct); // participants uniquement (404 sinon)
        var file = await media.OpenAttachmentAsync(attachment.Name, ct) ?? throw new NotFoundException("Pièce jointe");
        return new AttachmentDownload(file.Content, file.Length, file.ContentType, attachment.FileName);
    }

    /// <summary>Temps réel pour les participants connectés, notification (et push) pour les destinataires.</summary>
    private async Task<MessageDto> AnnounceAsync(Conversation conversation, Message message, CancellationToken ct)
    {
        var recipients = conversation.Participants.Select(p => p.UserId).Where(id => id != me.Id).ToList();
        var dto = message.ToDto();
        await live.MessageCreatedAsync(Participants(conversation), dto, ct);
        await bus.PublishAsync(new MessageSent(conversation.Id, me.Id, recipients, Text.Excerpt(message.Preview())), ct);
        return dto;
    }

    public async Task<MessageDto> Handle(SimulateReplyCommand request, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(request.ConversationId, ct);
        var other = conversation.Participants.First(p => p.UserId != me.Id).UserId;
        var message = conversation.Post(Guid.NewGuid(), other, request.Content, clock.GetUtcNow(), isSimulated: true);
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        var dto = message.ToDto();
        await live.MessageCreatedAsync(Participants(conversation), dto, ct);
        return dto;
    }

    public Task<MessageDto> Handle(ReactToMessageCommand request, CancellationToken ct) =>
        ChangeAsync(request.ConversationId, request.MessageId, m => m.React(me.Id, request.Emoji, clock.GetUtcNow()), ct);

    public Task<MessageDto> Handle(EditMessageCommand request, CancellationToken ct) =>
        ChangeAsync(request.ConversationId, request.MessageId, m => m.Edit(me.Id, request.Content, clock.GetUtcNow()), ct);

    public async Task<MessageDto> Handle(DeleteMessageCommand request, CancellationToken ct)
    {
        string? file = null;
        var dto = await ChangeAsync(request.ConversationId, request.MessageId, m => file = m.Delete(me.Id), ct);
        if (file is not null)
        {
            await bus.PublishAsync(new AttachmentsReleased([file]), ct);
        }

        return dto;
    }

    /// <summary>Modifie un message de la conversation puis prévient les participants connectés.</summary>
    private async Task<MessageDto> ChangeAsync(Guid conversationId, Guid messageId, Action<Message> change, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(conversationId, ct);
        var message = await db.Messages.Include(m => m.Reactions)
                          .FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId, ct)
                      ?? throw new NotFoundException("Message");

        change(message);
        await db.SaveChangesAsync(ct);
        var dto = message.ToDto();
        await live.MessageUpdatedAsync(Participants(conversation), dto, ct);
        return dto;
    }

    private static List<Guid> Participants(Conversation c) => c.Participants.Select(p => p.UserId).ToList();

    public async Task<int> Handle(MarkConversationReadCommand request, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(request.ConversationId, ct);
        var unread = await db.Messages
            .Where(m => m.ConversationId == request.ConversationId && m.SenderId != me.Id && !m.IsRead)
            .ToListAsync(ct);

        foreach (var message in unread)
        {
            message.MarkRead();
        }

        await db.SaveChangesAsync(ct);
        if (unread.Count > 0)
        {
            await live.ConversationReadAsync(Participants(conversation), conversation.Id, me.Id, ct);
        }

        return unread.Count;
    }

    public async Task<ConversationDto> Handle(StartDirectConversationCommand request, CancellationToken ct)
    {
        if (!await identity.ExistsAsync(request.OtherUserId, ct))
        {
            throw new NotFoundException("Membre");
        }

        await EnsureNotBlockedAsync([request.OtherUserId], ct);

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
        var mine = db.Conversations.SelectMany(c => c.Participants).Where(p => p.UserId == me.Id);
        return new CountDto(await db.Messages.CountAsync(m => m.SenderId != me.Id && !m.IsRead
            && mine.Any(p => p.ConversationId == m.ConversationId && (p.ClearedAt == null || m.SentAt > p.ClearedAt)), ct));
    }

    public async Task<CountDto> Handle(DeleteConversationCommand request, CancellationToken ct)
    {
        var conversation = await LoadMineAsync(request.ConversationId, ct);
        conversation.ClearFor(me.Id, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new CountDto(1);
    }

    private async Task EnsureNotBlockedAsync(IEnumerable<Guid> participants, CancellationToken ct)
    {
        var blocked = await identity.GetBlockedRelationsAsync(me.Id, ct);
        if (participants.Any(blocked.Contains))
        {
            throw new DomainException("Vous ne pouvez plus échanger de messages avec ce membre.");
        }
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
        var blocked = await identity.GetBlockedRelationsAsync(me.Id, ct);

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

            var since = c.ClearedAtFor(me.Id) ?? DateTimeOffset.MinValue;
            var list = (byConversation.GetValueOrDefault(c.Id) ?? []).Where(m => m.SentAt > since).ToList();
            result.Add(new ConversationSummaryDto(
                c.ToDto(),
                other,
                list.MaxBy(m => m.SentAt)?.ToDto(),
                list.Count(m => m.SenderId != me.Id && !m.IsRead),
                blocked.Contains(otherId),
                presence.IsOnline(otherId),
                presence.LastSeen(otherId)));
        }

        return result;
    }
}
