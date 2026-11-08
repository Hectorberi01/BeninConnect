using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Feed.Domain;

public enum PostVisibility
{
    Public,
    CommunityMembers,
    OnlyMe
}

public sealed class Post
{
    public const int MaxLength = 2000;

    private readonly List<PostLike> _likes = [];

    private Post()
    {
    }

    public Guid Id { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? CommunityId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? ImagePath { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public PostVisibility Visibility { get; private set; }
    public int CommentCount { get; private set; }
    public int ShareCount { get; private set; }
    public bool IsHidden { get; private set; }
    public string? HiddenReason { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }

    /// <summary>Masquée par la modération : invisible pour les membres, conservée pour l'historique.</summary>
    public void Hide(string reason)
    {
        IsHidden = true;
        HiddenReason = string.IsNullOrWhiteSpace(reason) ? "Contenu masqué par la modération" : reason.Trim();
    }

    public void Unhide()
    {
        IsHidden = false;
        HiddenReason = null;
    }

    public void CommentRemoved() => CommentCount = Math.Max(0, CommentCount - 1);

    /// <summary>Communauté supprimée : la publication reste, hors communauté.</summary>
    public void DetachFromCommunity() => CommunityId = null;

    public IReadOnlyCollection<PostLike> Likes => _likes;

    public static Post Create(Guid id, Guid authorId, Guid? communityId, string content, string? imagePath, PostVisibility visibility, DateTimeOffset now)
    {
        var text = Validate(content, visibility, communityId);
        return new Post
        {
            Id = id,
            AuthorId = authorId,
            CommunityId = communityId,
            Content = text,
            ImagePath = imagePath,
            Visibility = visibility,
            CreatedAt = now
        };
    }

    /// <summary>Modification par l'auteur : texte, photo et visibilité (la communauté ne change pas).</summary>
    public void Edit(string content, string? imagePath, PostVisibility visibility, DateTimeOffset now)
    {
        var text = Validate(content, visibility, CommunityId);
        if (text == Content && imagePath == ImagePath && visibility == Visibility)
        {
            return;
        }

        Content = text;
        ImagePath = imagePath;
        Visibility = visibility;
        EditedAt = now;
    }

    private static string Validate(string content, PostVisibility visibility, Guid? communityId)
    {
        var text = content.Trim();
        if (text.Length < 3)
        {
            throw new DomainException("La publication doit contenir au moins 3 caractères.");
        }

        if (text.Length > MaxLength)
        {
            throw new DomainException($"La publication ne doit pas dépasser {MaxLength} caractères.");
        }

        if (visibility == PostVisibility.CommunityMembers && communityId is null)
        {
            throw new DomainException("Choisissez une communauté pour une publication réservée aux membres.");
        }

        return text;
    }

    /// <summary>Retourne true si la publication est désormais aimée.</summary>
    public bool ToggleLike(Guid userId, DateTimeOffset now)
    {
        var existing = _likes.FirstOrDefault(l => l.UserId == userId);
        if (existing is not null)
        {
            _likes.Remove(existing);
            return false;
        }

        _likes.Add(new PostLike(Id, userId, now));
        return true;
    }

    public void Share() => ShareCount++;

    public Comment AddComment(Guid id, Guid authorId, string content, DateTimeOffset now)
    {
        var comment = Comment.Create(id, Id, authorId, content, now);
        CommentCount++;
        return comment;
    }

    /// <summary>Jeu de démonstration : compteurs initiaux.</summary>
    public void SeedCounters(int commentCount, int shareCount)
    {
        CommentCount = commentCount;
        ShareCount = shareCount;
    }

    public void SeedCreatedAt(DateTimeOffset createdAt) => CreatedAt = createdAt;
}

public sealed class PostLike
{
    private PostLike()
    {
    }

    public PostLike(Guid postId, Guid userId, DateTimeOffset likedAt)
    {
        PostId = postId;
        UserId = userId;
        LikedAt = likedAt;
    }

    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset LikedAt { get; private set; }
}

public sealed class Comment
{
    public const int MaxLength = 1000;

    private readonly List<CommentReaction> _reactions = [];

    private Comment()
    {
    }

    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<CommentReaction> Reactions => _reactions;

    /// <summary>Retourne true si le membre a désormais une réaction sur ce commentaire.</summary>
    public bool React(Guid userId, string? emoji, DateTimeOffset now) =>
        Reaction.Toggle(_reactions, userId, emoji, now, value => new CommentReaction(Id, userId, value, now));

    internal static Comment Create(Guid id, Guid postId, Guid authorId, string content, DateTimeOffset now)
    {
        var text = content.Trim();
        if (text.Length == 0)
        {
            throw new DomainException("Le commentaire est vide.");
        }

        if (text.Length > MaxLength)
        {
            throw new DomainException($"Le commentaire ne doit pas dépasser {MaxLength} caractères.");
        }

        return new Comment { Id = id, PostId = postId, AuthorId = authorId, Content = text, CreatedAt = now };
    }
}

public sealed class CommentReaction : Reaction
{
    private CommentReaction()
    {
    }

    public CommentReaction(Guid commentId, Guid userId, string emoji, DateTimeOffset now)
    {
        CommentId = commentId;
        UserId = userId;
        Emoji = emoji;
        ReactedAt = now;
    }

    public Guid CommentId { get; private set; }
}
