using BeninConnect.Shared.Application;

namespace BeninConnect.Modules.Feed.Contracts;

/// <summary>Publication vue par la console (noms résolus via Identity et Communities).</summary>
public sealed record AdminPostDto(
    Guid Id, Guid AuthorId, string AuthorName, Guid? CommunityId, string? CommunityName, string Content, string? ImagePath,
    DateTimeOffset CreatedAt, string Visibility, int LikeCount, int CommentCount, bool IsHidden, string? HiddenReason, int ReportCount);

/// <summary>Publication ou commentaire : aperçu pour la modération (PostId = publication parente).</summary>
public sealed record ContentPreview(Guid Id, string Content, Guid AuthorId, Guid PostId);

public sealed record FeedStats(int Posts, int HiddenPosts, int Comments, IReadOnlyList<DateTimeOffset> PostedSince);

public interface IFeedModule
{
    Task<IReadOnlyDictionary<Guid, ContentPreview>> GetPostPreviewsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, ContentPreview>> GetCommentPreviewsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    Task HidePostAsync(Guid postId, string reason, CancellationToken ct);

    Task DeletePostAsync(Guid postId, CancellationToken ct);

    Task DeleteCommentAsync(Guid commentId, CancellationToken ct);

    /// <summary>authorIds null : tous les auteurs.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountPostsByAuthorAsync(IReadOnlyCollection<Guid>? authorIds, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, int>> CountCommentsByAuthorAsync(IReadOnlyCollection<Guid>? authorIds, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, int>> CountPostsByCommunityAsync(CancellationToken ct);

    Task<IReadOnlyList<AdminPostDto>> GetRecentPostsForAdminAsync(Guid authorId, int take, CancellationToken ct);

    Task<FeedStats> GetStatsAsync(DateTimeOffset since, CancellationToken ct);

    Task<IReadOnlyList<DateTimeOffset>> GetCommentDatesAsync(DateTimeOffset since, CancellationToken ct);

    /// <summary>Publication visible par le membre connecté (partage en message privé).</summary>
    Task<bool> CanSeePostAsync(Guid postId, CancellationToken ct);
}

public sealed record PostLiked(Guid PostId, Guid PostAuthorId, Guid ActorId, string Excerpt) : IIntegrationEvent;

public sealed record PostCommented(Guid PostId, Guid PostAuthorId, Guid ActorId, string CommentExcerpt) : IIntegrationEvent;

/// <summary>Réponse à un commentaire : l'auteur du commentaire d'origine est prévenu.</summary>
public sealed record CommentReplied(Guid PostId, Guid ParentAuthorId, Guid ActorId, string Excerpt) : IIntegrationEvent;

/// <summary>Membres mentionnés (@) dans une publication ou un commentaire.</summary>
public sealed record MembersMentioned(Guid PostId, Guid ActorId, IReadOnlyList<Guid> UserIds, string Excerpt, bool InComment) : IIntegrationEvent;

public sealed record CommentReacted(Guid PostId, Guid CommentAuthorId, Guid ActorId, string Emoji, string CommentExcerpt) : IIntegrationEvent;
