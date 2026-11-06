using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Messaging.Domain;

public sealed class Conversation
{
    private readonly List<ConversationParticipant> _participants = [];

    private Conversation()
    {
    }

    public Guid Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<ConversationParticipant> Participants => _participants;

    public static Conversation CreateDirect(Guid id, Guid userId, Guid otherUserId, DateTimeOffset now)
    {
        if (userId == otherUserId)
        {
            throw new DomainException("Impossible de démarrer une conversation avec soi-même.");
        }

        var conversation = new Conversation { Id = id, CreatedAt = now };
        conversation._participants.Add(new ConversationParticipant(id, userId));
        conversation._participants.Add(new ConversationParticipant(id, otherUserId));
        return conversation;
    }

    public bool Includes(Guid userId) => _participants.Any(p => p.UserId == userId);

    public Message Post(Guid id, Guid senderId, string content, DateTimeOffset now, bool isSimulated = false)
    {
        if (!Includes(senderId))
        {
            throw new ForbiddenException("Vous ne participez pas à cette conversation.");
        }

        return Message.Create(id, Id, senderId, content, now, isSimulated);
    }
}

public sealed class ConversationParticipant
{
    private ConversationParticipant()
    {
    }

    public ConversationParticipant(Guid conversationId, Guid userId)
    {
        ConversationId = conversationId;
        UserId = userId;
    }

    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
}

public sealed class Message
{
    public const int MaxLength = 2000;

    private readonly List<MessageReaction> _reactions = [];

    private Message()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }
    public bool IsRead { get; private set; }
    public bool IsSimulated { get; private set; }

    public IReadOnlyCollection<MessageReaction> Reactions => _reactions;

    /// <summary>Retourne true si le membre a désormais une réaction sur ce message.</summary>
    public bool React(Guid userId, string? emoji, DateTimeOffset now) =>
        Reaction.Toggle(_reactions, userId, emoji, now, value => new MessageReaction(Id, userId, value, now));

    public static Message Create(Guid id, Guid conversationId, Guid senderId, string content, DateTimeOffset now, bool isSimulated = false, bool isRead = false)
    {
        var text = content.Trim();
        if (text.Length == 0)
        {
            throw new DomainException("Le message est vide.");
        }

        if (text.Length > MaxLength)
        {
            throw new DomainException($"Le message ne doit pas dépasser {MaxLength} caractères.");
        }

        return new Message
        {
            Id = id,
            ConversationId = conversationId,
            SenderId = senderId,
            Content = text,
            SentAt = now,
            IsSimulated = isSimulated,
            IsRead = isRead
        };
    }

    public void MarkRead() => IsRead = true;
}

public sealed class MessageReaction : Reaction
{
    private MessageReaction()
    {
    }

    public MessageReaction(Guid messageId, Guid userId, string emoji, DateTimeOffset now)
    {
        MessageId = messageId;
        UserId = userId;
        Emoji = emoji;
        ReactedAt = now;
    }

    public Guid MessageId { get; private set; }
}
