using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Feed.Domain;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Feed.Application;

public interface IFeedDb
{
    DbSet<Post> Posts { get; }
    DbSet<Comment> Comments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record PostDto(
    Guid Id, Guid AuthorId, Guid? CommunityId, string Content, string? ImagePath, DateTimeOffset CreatedAt,
    PostVisibility Visibility, IReadOnlyList<Guid> LikedBy, int CommentCount, int ShareCount, DateTimeOffset? EditedAt = null,
    string? VideoPath = null);

public sealed record CommentDto(Guid Id, Guid PostId, Guid AuthorId, string Content, DateTimeOffset CreatedAt, IReadOnlyList<ReactionDto> Reactions);

public static class FeedMapping
{
    /// <summary>Nécessite l'inclusion de Likes.</summary>
    public static PostDto ToDto(this Post p) => new(
        p.Id, p.AuthorId, p.CommunityId, p.Content, p.ImagePath, p.CreatedAt, p.Visibility,
        p.Likes.Select(l => l.UserId).ToList(), p.CommentCount, p.ShareCount, p.EditedAt, p.VideoPath);

    /// <summary>Fichiers envoyés par le membre (photo ou miniature, vidéo), à effacer avec la publication.</summary>
    public static List<string> Files(this Post p) => new[] { p.ImagePath, p.VideoPath }.OfType<string>().ToList();

    /// <summary>Nécessite l'inclusion de Reactions.</summary>
    public static CommentDto ToDto(this Comment c) => new(c.Id, c.PostId, c.AuthorId, c.Content, c.CreatedAt, c.Reactions.ToDto());
}

public sealed record GetFeedQuery(int Skip, int Take) : IQuery<PagedResult<PostDto>>;

public sealed record GetPostQuery(Guid Id) : IQuery<PostDto>;

public sealed record GetPostsQuery(Guid? AuthorId, Guid? CommunityId, string? Search) : IQuery<IReadOnlyList<PostDto>>;

public sealed record CreatePostCommand(Guid? CommunityId, string Content, string? ImagePath, PostVisibility Visibility, string? VideoPath = null)
    : ICommand<PostDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        Rules.Length(nameof(Content), Content, "La publication", 3, Post.MaxLength)
            .Concat(Rules.ImagePath(nameof(ImagePath), ImagePath))
            .Concat(Rules.VideoPath(nameof(VideoPath), VideoPath));
}

public sealed record ToggleLikeCommand(Guid PostId) : ICommand<PostDto>;

public sealed record SharePostCommand(Guid PostId) : ICommand<PostDto>;

public sealed record GetCommentsQuery(Guid PostId) : IQuery<IReadOnlyList<CommentDto>>;

public sealed record ReactToCommentCommand(Guid PostId, Guid CommentId, string Emoji) : ICommand<CommentDto>;

public sealed record AddCommentCommand(Guid PostId, string Content) : ICommand<CommentDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() => Rules.Length(nameof(Content), Content, "Le commentaire", 1, Comment.MaxLength);
}

/// <summary>Publications visibles : publiques, celles des communautés dont on est membre, et les siennes ; jamais celles d'un membre bloqué.</summary>
public sealed class PostAccess(IFeedDb db, ICommunitiesModule communities, IIdentityModule identity, ICurrentUser me)
{
    /// <summary>Auteurs masqués pour le membre connecté (blocage dans un sens ou dans l'autre).</summary>
    public async Task<List<Guid>> BlockedAuthorsAsync(CancellationToken ct) => [.. await identity.GetBlockedRelationsAsync(me.Id, ct)];

    public async Task<IQueryable<Post>> VisibleAsync(IQueryable<Post> posts, CancellationToken ct)
    {
        var memberOf = await communities.GetCommunityIdsOfMemberAsync(me.Id, ct);
        var blocked = await BlockedAuthorsAsync(ct);
        var id = me.Id;
        return posts.Where(p => !p.IsHidden && !blocked.Contains(p.AuthorId) && (
            p.AuthorId == id
            || p.Visibility == PostVisibility.Public
            || (p.Visibility == PostVisibility.CommunityMembers && p.CommunityId != null && memberOf.Contains(p.CommunityId.Value))));
    }

    public async Task<Post> GetVisibleAsync(Guid id, CancellationToken ct) =>
        await (await VisibleAsync(db.Posts.Include(p => p.Likes), ct)).FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException("Publication");
}

public sealed class PostQueryHandlers(IFeedDb db, PostAccess access) :
    IRequestHandler<GetFeedQuery, PagedResult<PostDto>>,
    IRequestHandler<GetPostQuery, PostDto>,
    IRequestHandler<GetPostsQuery, IReadOnlyList<PostDto>>,
    IRequestHandler<GetCommentsQuery, IReadOnlyList<CommentDto>>
{
    public async Task<PagedResult<PostDto>> Handle(GetFeedQuery request, CancellationToken ct)
    {
        var take = Math.Clamp(request.Take, 1, 50);
        var skip = Math.Max(0, request.Skip);
        var visible = await access.VisibleAsync(db.Posts.AsNoTracking(), ct);

        var total = await visible.CountAsync(ct);
        var items = await visible.Include(p => p.Likes)
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return new PagedResult<PostDto>(items.Select(p => p.ToDto()).ToList(), total, skip);
    }

    public async Task<PostDto> Handle(GetPostQuery request, CancellationToken ct) =>
        (await access.GetVisibleAsync(request.Id, ct)).ToDto();

    public async Task<IReadOnlyList<PostDto>> Handle(GetPostsQuery request, CancellationToken ct)
    {
        var query = await access.VisibleAsync(db.Posts.AsNoTracking().Include(p => p.Likes), ct);
        if (request.AuthorId is { } author)
        {
            query = query.Where(p => p.AuthorId == author);
        }

        if (request.CommunityId is { } community)
        {
            query = query.Where(p => p.CommunityId == community);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.Trim().ToLower();
            query = query.Where(p => p.Content.ToLower().Contains(q));
        }

        var limit = string.IsNullOrWhiteSpace(request.Search) ? 200 : 30;
        return (await query.OrderByDescending(p => p.CreatedAt).Take(limit).ToListAsync(ct)).Select(p => p.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<CommentDto>> Handle(GetCommentsQuery request, CancellationToken ct)
    {
        await access.GetVisibleAsync(request.PostId, ct);
        var blocked = await access.BlockedAuthorsAsync(ct);
        return (await db.Comments.AsNoTracking().Include(c => c.Reactions)
                .Where(c => c.PostId == request.PostId && !blocked.Contains(c.AuthorId)).OrderBy(c => c.CreatedAt).ToListAsync(ct))
            .Select(c => c.ToDto()).ToList();
    }
}

public sealed class PostCommandHandlers(IFeedDb db, PostAccess access, ICommunitiesModule communities, IEventBus bus, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<CreatePostCommand, PostDto>,
    IRequestHandler<ToggleLikeCommand, PostDto>,
    IRequestHandler<SharePostCommand, PostDto>,
    IRequestHandler<AddCommentCommand, CommentDto>,
    IRequestHandler<ReactToCommentCommand, CommentDto>
{
    public async Task<PostDto> Handle(CreatePostCommand request, CancellationToken ct)
    {
        if (request.CommunityId is { } communityId)
        {
            if (!await communities.IsMemberAsync(communityId, me.Id, ct))
            {
                throw new ForbiddenException("Rejoignez la communauté pour y publier.");
            }
        }

        var post = Post.Create(Guid.NewGuid(), me.Id, request.CommunityId, request.Content, request.ImagePath, request.Visibility, clock.GetUtcNow(),
            string.IsNullOrWhiteSpace(request.VideoPath) ? null : request.VideoPath);
        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
        return post.ToDto();
    }

    public async Task<PostDto> Handle(ToggleLikeCommand request, CancellationToken ct)
    {
        var post = await access.GetVisibleAsync(request.PostId, ct);
        var liked = post.ToggleLike(me.Id, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        if (liked && post.AuthorId != me.Id)
        {
            await bus.PublishAsync(new PostLiked(post.Id, post.AuthorId, me.Id, Text.Excerpt(post.Content)), ct);
        }

        return post.ToDto();
    }

    public async Task<PostDto> Handle(SharePostCommand request, CancellationToken ct)
    {
        var post = await access.GetVisibleAsync(request.PostId, ct);
        post.Share();
        await db.SaveChangesAsync(ct);
        return post.ToDto();
    }

    public async Task<CommentDto> Handle(AddCommentCommand request, CancellationToken ct)
    {
        var post = await access.GetVisibleAsync(request.PostId, ct);
        var comment = post.AddComment(Guid.NewGuid(), me.Id, request.Content, clock.GetUtcNow());
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);

        if (post.AuthorId != me.Id)
        {
            await bus.PublishAsync(new PostCommented(post.Id, post.AuthorId, me.Id, Text.Excerpt(comment.Content)), ct);
        }

        return comment.ToDto();
    }

    public async Task<CommentDto> Handle(ReactToCommentCommand request, CancellationToken ct)
    {
        await access.GetVisibleAsync(request.PostId, ct);
        var comment = await db.Comments.Include(c => c.Reactions)
                          .FirstOrDefaultAsync(c => c.Id == request.CommentId && c.PostId == request.PostId, ct)
                      ?? throw new NotFoundException("Commentaire");

        var reacted = comment.React(me.Id, request.Emoji, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        if (reacted && comment.AuthorId != me.Id)
        {
            await bus.PublishAsync(new CommentReacted(request.PostId, comment.AuthorId, me.Id, request.Emoji.Trim(), Text.Excerpt(comment.Content)), ct);
        }

        return comment.ToDto();
    }
}
