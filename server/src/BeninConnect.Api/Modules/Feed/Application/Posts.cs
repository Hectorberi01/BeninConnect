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
    DbSet<SavedPost> SavedPosts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Images : carrousel (ImagePath = la première). MentionIds : membres mentionnés (@Prénom Nom dans le texte).</summary>
public sealed record PostDto(
    Guid Id, Guid AuthorId, Guid? CommunityId, string Content, string? ImagePath, DateTimeOffset CreatedAt,
    PostVisibility Visibility, IReadOnlyList<Guid> LikedBy, int CommentCount, int ShareCount, DateTimeOffset? EditedAt = null,
    string? VideoPath = null, IReadOnlyList<string>? Images = null, IReadOnlyList<Guid>? MentionIds = null,
    DateTimeOffset? PinnedAt = null, Guid? PinnedCommentId = null);

/// <summary>ParentId : réponse à ce commentaire.</summary>
public sealed record CommentDto(
    Guid Id, Guid PostId, Guid AuthorId, string Content, DateTimeOffset CreatedAt, IReadOnlyList<ReactionDto> Reactions,
    Guid? ParentId = null, IReadOnlyList<Guid>? MentionIds = null);

public sealed record SaveStateDto(Guid PostId, bool Saved);

public static class FeedMapping
{
    /// <summary>Nécessite l'inclusion de Likes.</summary>
    public static PostDto ToDto(this Post p) => new(
        p.Id, p.AuthorId, p.CommunityId, p.Content, p.ImagePath, p.CreatedAt, p.Visibility,
        p.Likes.Select(l => l.UserId).ToList(), p.CommentCount, p.ShareCount, p.EditedAt, p.VideoPath,
        p.Images.Count > 0 ? p.Images : p.ImagePath is { } image && p.VideoPath is null ? [image] : [],
        p.MentionIds, p.PinnedAt, p.PinnedCommentId);

    /// <summary>Fichiers envoyés par le membre (photos ou miniature, vidéo), à effacer avec la publication.</summary>
    public static List<string> Files(this Post p) => new[] { p.ImagePath, p.VideoPath }.Concat(p.Images).OfType<string>().Distinct().ToList();

    /// <summary>Nécessite l'inclusion de Reactions.</summary>
    public static CommentDto ToDto(this Comment c) =>
        new(c.Id, c.PostId, c.AuthorId, c.Content, c.CreatedAt, c.Reactions.ToDto(), c.ParentId, c.MentionIds);
}

public sealed record GetFeedQuery(int Skip, int Take) : IQuery<PagedResult<PostDto>>;

public sealed record GetPostQuery(Guid Id) : IQuery<PostDto>;

/// <summary>Tag : publications portant ce hashtag (sans #). Avec AuthorId : les publications épinglées d'abord.</summary>
public sealed record GetPostsQuery(Guid? AuthorId, Guid? CommunityId, string? Search, string? Tag = null) : IQuery<IReadOnlyList<PostDto>>;

public sealed record CreatePostCommand(
    Guid? CommunityId, string Content, string? ImagePath, PostVisibility Visibility, string? VideoPath = null,
    IReadOnlyList<string>? Images = null, IReadOnlyList<Guid>? Mentions = null)
    : ICommand<PostDto>, IValidatable
{
    public IEnumerable<(string, string)> Validate() =>
        Rules.Length(nameof(Content), Content, "La publication", 3, Post.MaxLength)
            .Concat(Rules.ImagePath(nameof(ImagePath), ImagePath))
            .Concat(Rules.VideoPath(nameof(VideoPath), VideoPath))
            .Concat(PostRules.Images(Images));
}

public static class PostRules
{
    public static IEnumerable<(string, string)> Images(IReadOnlyList<string>? images)
    {
        if (images is null)
        {
            yield break;
        }

        if (images.Count > Post.MaxImages)
        {
            yield return (nameof(Images), $"Une publication contient au plus {Post.MaxImages} photos.");
        }

        foreach (var error in images.SelectMany(i => Rules.ImagePath(nameof(Images), i)))
        {
            yield return error;
        }
    }
}

public sealed record ToggleSaveCommand(Guid PostId) : ICommand<SaveStateDto>;

/// <summary>Publications enregistrées, de la plus récemment enregistrée à la plus ancienne.</summary>
public sealed record GetSavedPostsQuery : IQuery<IReadOnlyList<PostDto>>;

public sealed record GetSavedPostIdsQuery : IQuery<IReadOnlyList<Guid>>;

public sealed record PinPostCommand(Guid PostId, bool Pinned) : ICommand<PostDto>;

/// <summary>CommentId null : désépingler.</summary>
public sealed record PinCommentCommand(Guid PostId, Guid? CommentId) : ICommand<PostDto>;

public sealed record ToggleLikeCommand(Guid PostId) : ICommand<PostDto>;

public sealed record SharePostCommand(Guid PostId) : ICommand<PostDto>;

public sealed record GetCommentsQuery(Guid PostId) : IQuery<IReadOnlyList<CommentDto>>;

public sealed record ReactToCommentCommand(Guid PostId, Guid CommentId, string Emoji) : ICommand<CommentDto>;

public sealed record AddCommentCommand(Guid PostId, string Content, Guid? ParentId = null, IReadOnlyList<Guid>? Mentions = null) : ICommand<CommentDto>, IValidatable
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

        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            var tag = SocialText.NormalizeTag(request.Tag);
            query = query.Where(p => p.Tags.Contains(tag));
        }

        var limit = string.IsNullOrWhiteSpace(request.Search) ? 200 : 30;
        var ordered = request.AuthorId is not null
            ? query.OrderByDescending(p => p.PinnedAt != null).ThenByDescending(p => p.PinnedAt).ThenByDescending(p => p.CreatedAt)
            : query.OrderByDescending(p => p.CreatedAt);
        return (await ordered.Take(limit).ToListAsync(ct)).Select(p => p.ToDto()).ToList();
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

public sealed class PostCommandHandlers(
    IFeedDb db, PostAccess access, ICommunitiesModule communities, IIdentityModule identity, IEventBus bus, ICurrentUser me, TimeProvider clock) :
    IRequestHandler<CreatePostCommand, PostDto>,
    IRequestHandler<ToggleSaveCommand, SaveStateDto>,
    IRequestHandler<GetSavedPostsQuery, IReadOnlyList<PostDto>>,
    IRequestHandler<GetSavedPostIdsQuery, IReadOnlyList<Guid>>,
    IRequestHandler<PinPostCommand, PostDto>,
    IRequestHandler<PinCommentCommand, PostDto>,
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
            string.IsNullOrWhiteSpace(request.VideoPath) ? null : request.VideoPath, request.Images, await ValidMentionsAsync(request.Mentions, ct));
        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
        await AnnounceMentionsAsync(post.Id, post.MentionIds, post.Content, inComment: false, ct);
        return post.ToDto();
    }

    /// <summary>Membres existants, ni soi-même ni bloqués (dans un sens ou dans l'autre).</summary>
    private async Task<List<Guid>> ValidMentionsAsync(IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        var wanted = SocialText.Mentions(ids, me.Id);
        if (wanted.Count == 0)
        {
            return wanted;
        }

        var existing = await identity.GetNamesAsync(wanted, ct);
        var blocked = await identity.GetBlockedRelationsAsync(me.Id, ct);
        return wanted.Where(id => existing.ContainsKey(id) && !blocked.Contains(id)).ToList();
    }

    private async Task AnnounceMentionsAsync(Guid postId, IReadOnlyList<Guid> mentioned, string text, bool inComment, CancellationToken ct, IEnumerable<Guid>? already = null)
    {
        var fresh = mentioned.Except(already ?? []).ToList();
        if (fresh.Count > 0)
        {
            await bus.PublishAsync(new MembersMentioned(postId, me.Id, fresh, Text.Excerpt(text), inComment), ct);
        }
    }

    public async Task<SaveStateDto> Handle(ToggleSaveCommand request, CancellationToken ct)
    {
        var existing = await db.SavedPosts.FirstOrDefaultAsync(s => s.UserId == me.Id && s.PostId == request.PostId, ct);
        if (existing is not null)
        {
            db.SavedPosts.Remove(existing);
            await db.SaveChangesAsync(ct);
            return new SaveStateDto(request.PostId, false);
        }

        await access.GetVisibleAsync(request.PostId, ct);
        db.SavedPosts.Add(new SavedPost(me.Id, request.PostId, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return new SaveStateDto(request.PostId, true);
    }

    public async Task<IReadOnlyList<PostDto>> Handle(GetSavedPostsQuery request, CancellationToken ct)
    {
        var saved = await db.SavedPosts.AsNoTracking().Where(s => s.UserId == me.Id).OrderByDescending(s => s.SavedAt)
            .Select(s => s.PostId).Take(200).ToListAsync(ct);
        var visible = await (await access.VisibleAsync(db.Posts.AsNoTracking().Include(p => p.Likes), ct))
            .Where(p => saved.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        return saved.Where(visible.ContainsKey).Select(id => visible[id].ToDto()).ToList();
    }

    public async Task<IReadOnlyList<Guid>> Handle(GetSavedPostIdsQuery request, CancellationToken ct) =>
        await db.SavedPosts.AsNoTracking().Where(s => s.UserId == me.Id).Select(s => s.PostId).ToListAsync(ct);

    public async Task<PostDto> Handle(PinPostCommand request, CancellationToken ct)
    {
        var post = await OwnPostAsync(request.PostId, "épingler", ct);
        if (request.Pinned && post.PinnedAt is null
            && await db.Posts.CountAsync(p => p.AuthorId == me.Id && p.PinnedAt != null, ct) >= Post.MaxPinned)
        {
            throw new DomainException($"Vous pouvez épingler au plus {Post.MaxPinned} publications : désépinglez-en une d'abord.");
        }

        if (request.Pinned)
        {
            post.Pin(clock.GetUtcNow());
        }
        else
        {
            post.Unpin();
        }

        await db.SaveChangesAsync(ct);
        return post.ToDto();
    }

    public async Task<PostDto> Handle(PinCommentCommand request, CancellationToken ct)
    {
        var post = await OwnPostAsync(request.PostId, "épingler un commentaire de", ct);
        if (request.CommentId is { } id && !await db.Comments.AnyAsync(c => c.Id == id && c.PostId == post.Id, ct))
        {
            throw new NotFoundException("Commentaire");
        }

        post.PinComment(request.CommentId);
        await db.SaveChangesAsync(ct);
        return post.ToDto();
    }

    private async Task<Post> OwnPostAsync(Guid id, string action, CancellationToken ct)
    {
        var post = await db.Posts.Include(p => p.Likes).FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Publication");
        return post.AuthorId == me.Id ? post : throw new ForbiddenException($"Vous ne pouvez {action} que vos propres publications.");
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
        Comment? parent = null;
        if (request.ParentId is { } parentId)
        {
            parent = await db.Comments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == parentId && c.PostId == post.Id, ct)
                     ?? throw new NotFoundException("Commentaire");
            if (parent.ParentId is { } root)
            {
                // Réponse à une réponse : rattachée au commentaire d'origine (un seul niveau)
                parent = await db.Comments.AsNoTracking().FirstAsync(c => c.Id == root, ct);
            }
        }

        var comment = post.AddComment(Guid.NewGuid(), me.Id, request.Content, clock.GetUtcNow(), parent?.Id, await ValidMentionsAsync(request.Mentions, ct));
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);

        var excerpt = Text.Excerpt(comment.Content);
        if (post.AuthorId != me.Id)
        {
            await bus.PublishAsync(new PostCommented(post.Id, post.AuthorId, me.Id, excerpt), ct);
        }

        if (parent is not null && parent.AuthorId != me.Id && parent.AuthorId != post.AuthorId)
        {
            await bus.PublishAsync(new CommentReplied(post.Id, parent.AuthorId, me.Id, excerpt), ct);
        }

        // Déjà prévenus par le commentaire ou la réponse : pas de seconde notification
        await AnnounceMentionsAsync(post.Id, comment.MentionIds, comment.Content, inComment: true, ct,
            new[] { post.AuthorId, parent?.AuthorId ?? Guid.Empty });
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
