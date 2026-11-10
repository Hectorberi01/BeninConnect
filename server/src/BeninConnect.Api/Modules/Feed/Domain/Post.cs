using System.Text.RegularExpressions;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Feed.Domain;

public enum PostVisibility
{
    Public,
    CommunityMembers,
    OnlyMe
}

/// <summary>Hashtags (#Cotonou) et mentions (identifiants de membres) d'un texte.</summary>
public static partial class SocialText
{
    public const int MaxTags = 20;
    public const int MaxMentions = 20;

    /// <summary>Hashtags en minuscules, sans le #, sans doublon.</summary>
    public static List<string> Tags(string text) =>
        TagRegex().Matches(text).Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().Take(MaxTags).ToList();

    public static string NormalizeTag(string tag) => tag.Trim().TrimStart('#').ToLowerInvariant();

    public static List<Guid> Mentions(IEnumerable<Guid>? ids, Guid authorId) =>
        (ids ?? []).Where(id => id != Guid.Empty && id != authorId).Distinct().Take(MaxMentions).ToList();

    [GeneratedRegex(@"(?<![\p{L}\p{N}_])#([\p{L}\p{N}_]{2,40})")]
    private static partial Regex TagRegex();
}

public sealed class Post
{
    public const int MaxLength = 2000;
    public const int MaxImages = 10;
    public const int MaxPinned = 3;

    private readonly List<PostLike> _likes = [];

    private Post()
    {
    }

    public Guid Id { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? CommunityId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    /// <summary>Photo, ou miniature de la vidéo.</summary>
    public string? ImagePath { get; private set; }
    public string? VideoPath { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public PostVisibility Visibility { get; private set; }
    public int CommentCount { get; private set; }
    public int ShareCount { get; private set; }
    public bool IsHidden { get; private set; }
    public string? HiddenReason { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }

    /// <summary>Photos du carrousel, dans l'ordre (ImagePath = la première, pour l'aperçu).</summary>
    public List<string> Images { get; private set; } = [];

    /// <summary>Hashtags du texte, en minuscules.</summary>
    public List<string> Tags { get; private set; } = [];

    public List<Guid> MentionIds { get; private set; } = [];

    /// <summary>Épinglée en haut du profil de l'auteur.</summary>
    public DateTimeOffset? PinnedAt { get; private set; }

    /// <summary>Commentaire épinglé par l'auteur sous sa publication.</summary>
    public Guid? PinnedCommentId { get; private set; }

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

    public static Post Create(
        Guid id, Guid authorId, Guid? communityId, string content, string? imagePath, PostVisibility visibility, DateTimeOffset now, string? videoPath = null,
        IReadOnlyList<string>? images = null, IEnumerable<Guid>? mentions = null)
    {
        var text = Validate(content, visibility, communityId);
        var post = new Post
        {
            Id = id,
            AuthorId = authorId,
            CommunityId = communityId,
            Content = text,
            VideoPath = videoPath,
            Visibility = visibility,
            CreatedAt = now,
            Tags = SocialText.Tags(text),
            MentionIds = SocialText.Mentions(mentions, authorId)
        };
        post.SetMedia(imagePath, videoPath, images);
        return post;
    }

    /// <summary>Modification par l'auteur : texte, photos ou vidéo, visibilité (la communauté ne change pas).</summary>
    public void Edit(string content, string? imagePath, PostVisibility visibility, DateTimeOffset now, string? videoPath = null,
        IReadOnlyList<string>? images = null, IEnumerable<Guid>? mentions = null)
    {
        var text = Validate(content, visibility, CommunityId);
        var before = (Content, ImagePath, VideoPath, Visibility, string.Join('|', Images), string.Join('|', MentionIds));
        Content = text;
        Visibility = visibility;
        Tags = SocialText.Tags(text);
        MentionIds = SocialText.Mentions(mentions, AuthorId);
        SetMedia(imagePath, videoPath, images);
        if (before != (Content, ImagePath, VideoPath, Visibility, string.Join('|', Images), string.Join('|', MentionIds)))
        {
            EditedAt = now;
        }
    }

    /// <summary>
    /// Vidéo : ImagePath = miniature, pas de carrousel. Sinon, Images (jusqu'à 10) ; ImagePath = la première.
    /// Un seul ImagePath sans Images (anciennes versions de l'app) devient un carrousel d'une photo.
    /// </summary>
    private void SetMedia(string? imagePath, string? videoPath, IReadOnlyList<string>? images)
    {
        VideoPath = videoPath;
        if (videoPath is not null)
        {
            ImagePath = imagePath;
            Images = [];
            return;
        }

        var list = (images is { Count: > 0 } ? images : imagePath is null ? [] : [imagePath])
            .Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToList();
        if (list.Count > MaxImages)
        {
            throw new DomainException($"Une publication contient au plus {MaxImages} photos.");
        }

        Images = list;
        ImagePath = list.FirstOrDefault();
    }

    /// <summary>L'auteur épingle ou désépingle sa publication en haut de son profil.</summary>
    public void Pin(DateTimeOffset now) => PinnedAt ??= now;

    public void Unpin() => PinnedAt = null;

    public void PinComment(Guid? commentId) => PinnedCommentId = commentId;

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

    public Comment AddComment(Guid id, Guid authorId, string content, DateTimeOffset now, Guid? parentId = null, IEnumerable<Guid>? mentions = null)
    {
        var comment = Comment.Create(id, Id, authorId, content, now, parentId, mentions);
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

    /// <summary>Réponse à ce commentaire (un seul niveau : une réponse à une réponse vise le commentaire d'origine).</summary>
    public Guid? ParentId { get; private set; }

    public List<Guid> MentionIds { get; private set; } = [];

    public IReadOnlyCollection<CommentReaction> Reactions => _reactions;

    /// <summary>Retourne true si le membre a désormais une réaction sur ce commentaire.</summary>
    public bool React(Guid userId, string? emoji, DateTimeOffset now) =>
        Reaction.Toggle(_reactions, userId, emoji, now, value => new CommentReaction(Id, userId, value, now));

    internal static Comment Create(Guid id, Guid postId, Guid authorId, string content, DateTimeOffset now, Guid? parentId, IEnumerable<Guid>? mentions)
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

        return new Comment
        {
            Id = id, PostId = postId, AuthorId = authorId, Content = text, CreatedAt = now, ParentId = parentId,
            MentionIds = SocialText.Mentions(mentions, authorId)
        };
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

/// <summary>Publication enregistrée par un membre (onglet « Enregistrés »).</summary>
public sealed class SavedPost
{
    private SavedPost()
    {
    }

    public SavedPost(Guid userId, Guid postId, DateTimeOffset savedAt)
    {
        UserId = userId;
        PostId = postId;
        SavedAt = savedAt;
    }

    public Guid UserId { get; private set; }
    public Guid PostId { get; private set; }
    public DateTimeOffset SavedAt { get; private set; }
}
