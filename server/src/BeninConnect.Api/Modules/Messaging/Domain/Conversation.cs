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

    public Message Post(Guid id, Guid senderId, string content, DateTimeOffset now, bool isSimulated = false, MessageAttachment? attachment = null,
        Guid? sharedPostId = null)
    {
        if (!Includes(senderId))
        {
            throw new ForbiddenException("Vous ne participez pas à cette conversation.");
        }

        return Message.Create(id, Id, senderId, content, now, isSimulated, attachment: attachment, sharedPostId: sharedPostId);
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

public enum AttachmentKind
{
    Image,
    Audio,
    File
}

/// <summary>Pièce jointe privée (photo, PDF, message vocal) ; Name : nom du fichier dans le stockage privé.</summary>
public sealed record MessageAttachment(string Name, AttachmentKind Kind, string FileName, string ContentType, long Size, int? DurationMs)
{
    public const int MaxFileNameLength = 120;

    /// <summary>Nom affiché : sans chemin, ni caractère de contrôle, 120 caractères au plus.</summary>
    public static string CleanFileName(string? fileName, string fallback)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        name = new string(name.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        if (name.Length == 0)
        {
            return fallback;
        }

        return name.Length <= MaxFileNameLength ? name : name[..MaxFileNameLength];
    }
}

public sealed class Message
{
    public const int MaxLength = 2000;

    /// <summary>Délai pendant lequel l'expéditeur peut modifier son message.</summary>
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(15);

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
    public DateTimeOffset? EditedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public MessageAttachment? Attachment { get; private set; }

    /// <summary>Publication partagée dans la discussion (aperçu chargé par l'application).</summary>
    public Guid? SharedPostId { get; private set; }

    public IReadOnlyCollection<MessageReaction> Reactions => _reactions;

    /// <summary>Retourne true si le membre a désormais une réaction sur ce message.</summary>
    public bool React(Guid userId, string? emoji, DateTimeOffset now) =>
        IsDeleted ? throw new DomainException("Ce message a été supprimé.") : Reaction.Toggle(_reactions, userId, emoji, now, value => new MessageReaction(Id, userId, value, now));

    public static Message Create(
        Guid id, Guid conversationId, Guid senderId, string content, DateTimeOffset now, bool isSimulated = false, bool isRead = false,
        MessageAttachment? attachment = null, Guid? sharedPostId = null)
    {
        // Avec une pièce jointe ou une publication partagée, le texte est facultatif.
        var text = (attachment is not null || sharedPostId is not null) && string.IsNullOrWhiteSpace(content) ? string.Empty : Normalize(content);
        return new Message
        {
            Id = id,
            ConversationId = conversationId,
            SenderId = senderId,
            Content = text,
            SentAt = now,
            IsSimulated = isSimulated,
            IsRead = isRead,
            Attachment = attachment,
            SharedPostId = sharedPostId
        };
    }

    public void MarkRead() => IsRead = true;

    /// <summary>Par l'expéditeur, dans les 15 minutes suivant l'envoi.</summary>
    public void Edit(Guid userId, string content, DateTimeOffset now)
    {
        EnsureSender(userId, "modifier");
        if (now - SentAt > EditWindow)
        {
            throw new DomainException("Un message ne peut plus être modifié 15 minutes après son envoi.");
        }

        var text = (Attachment is not null || SharedPostId is not null) && string.IsNullOrWhiteSpace(content) ? string.Empty : Normalize(content);
        if (text != Content)
        {
            Content = text;
            EditedAt = now;
        }
    }

    /// <summary>
    /// Par l'expéditeur : contenu, pièce jointe et réactions effacés, la place du message reste (« Message supprimé »).
    /// Retourne le fichier de la pièce jointe à effacer du stockage.
    /// </summary>
    public string? Delete(Guid userId)
    {
        EnsureSender(userId, "supprimer");
        var file = Attachment?.Name;
        IsDeleted = true;
        Content = string.Empty;
        Attachment = null;
        _reactions.Clear();
        return file;
    }

    private void EnsureSender(Guid userId, string action)
    {
        if (IsDeleted)
        {
            throw new DomainException("Ce message a été supprimé.");
        }

        if (SenderId != userId)
        {
            throw new ForbiddenException($"Vous ne pouvez {action} que vos propres messages.");
        }
    }

    private static string Normalize(string content)
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

        return text;
    }
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
