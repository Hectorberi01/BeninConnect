using BeninConnect.Modules.Administration.Contracts;
using BeninConnect.Modules.Communities.Contracts;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Feed.Domain;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Moderation.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Feed.Application;

public sealed record AdminCommentDto(Guid Id, Guid PostId, Guid AuthorId, string AuthorName, string Content, DateTimeOffset CreatedAt);

/// <summary>Status : all, visible, hidden, reported.</summary>
public sealed record GetAdminPostsQuery(string? Search, string? Status, Guid? AuthorId, int Skip = 0, int Take = 25) : IQuery<PagedResult<AdminPostDto>>;

public sealed record GetAdminCommentsQuery(Guid PostId) : IQuery<IReadOnlyList<AdminCommentDto>>;

public sealed record HidePostCommand(Guid Id, string? Reason) : ICommand<AdminPostDto>;

public sealed record UnhidePostCommand(Guid Id) : ICommand<AdminPostDto>;

public sealed record DeletePostCommand(Guid Id) : ICommand<bool>;

public sealed record DeleteCommentCommand(Guid Id) : ICommand<bool>;

/// <summary>Publications vues par la console : noms et nombre de signalements obtenus via les contrats des autres modules.</summary>
public sealed class AdminPostReader(IIdentityModule identity, ICommunitiesModule communities, IModerationModule moderation)
{
    public async Task<List<AdminPostDto>> DescribeAsync(IReadOnlyList<Post> posts, CancellationToken ct)
    {
        var authors = await identity.GetNamesAsync(posts.Select(p => p.AuthorId), ct);
        var groups = await communities.GetInfosAsync(posts.Where(p => p.CommunityId != null).Select(p => p.CommunityId!.Value), ct);
        var reports = await moderation.CountReportsAsync(ReportTarget.Post, posts.Select(p => p.Id).ToList(), ct);
        return posts.Select(p => new AdminPostDto(
            p.Id, p.AuthorId, authors.GetValueOrDefault(p.AuthorId) ?? "Membre supprimé",
            p.CommunityId, p.CommunityId is { } c ? groups.GetValueOrDefault(c)?.Name : null,
            p.Content, p.ImagePath, p.CreatedAt, p.Visibility.ToString(), p.Likes.Count, p.CommentCount, p.IsHidden, p.HiddenReason,
            reports.GetValueOrDefault(p.Id))).ToList();
    }
}

public sealed class AdminFeedHandlers(IFeedDb db, AdminPostReader reader, IIdentityModule identity, IModerationModule moderation, IAdminSession admin) :
    IRequestHandler<GetAdminPostsQuery, PagedResult<AdminPostDto>>,
    IRequestHandler<GetAdminCommentsQuery, IReadOnlyList<AdminCommentDto>>,
    IRequestHandler<HidePostCommand, AdminPostDto>,
    IRequestHandler<UnhidePostCommand, AdminPostDto>,
    IRequestHandler<DeletePostCommand, bool>,
    IRequestHandler<DeleteCommentCommand, bool>
{
    public async Task<PagedResult<AdminPostDto>> Handle(GetAdminPostsQuery request, CancellationToken ct)
    {
        var (skip, take) = Text.Page(request.Skip, request.Take);
        var posts = db.Posts.AsNoTracking();
        if (Text.Like(request.Search) is { } q)
        {
            posts = posts.Where(p => p.Content.ToLower().Contains(q));
        }

        if (request.AuthorId is { } author)
        {
            posts = posts.Where(p => p.AuthorId == author);
        }

        if (request.Status == "reported")
        {
            var reported = await moderation.GetOpenTargetIdsAsync(ReportTarget.Post, ct);
            posts = posts.Where(p => reported.Contains(p.Id));
        }

        posts = request.Status switch
        {
            "visible" => posts.Where(p => !p.IsHidden),
            "hidden" => posts.Where(p => p.IsHidden),
            _ => posts
        };

        var total = await posts.CountAsync(ct);
        var page = await posts.Include(p => p.Likes).OrderByDescending(p => p.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AdminPostDto>(await reader.DescribeAsync(page, ct), total, skip);
    }

    public async Task<IReadOnlyList<AdminCommentDto>> Handle(GetAdminCommentsQuery request, CancellationToken ct)
    {
        var comments = await db.Comments.AsNoTracking().Where(c => c.PostId == request.PostId).OrderBy(c => c.CreatedAt).ToListAsync(ct);
        var names = await identity.GetNamesAsync(comments.Select(c => c.AuthorId), ct);
        return comments.Select(c => new AdminCommentDto(c.Id, c.PostId, c.AuthorId, names.GetValueOrDefault(c.AuthorId) ?? "Membre supprimé", c.Content, c.CreatedAt))
            .ToList();
    }

    public async Task<AdminPostDto> Handle(HidePostCommand request, CancellationToken ct)
    {
        var post = await LoadAsync(request.Id, ct);
        post.Hide(request.Reason ?? string.Empty);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Publication masquée", "Post", post.Id, $"{post.HiddenReason} — « {Text.Excerpt(post.Content)} »", ct);
        return (await reader.DescribeAsync([post], ct))[0];
    }

    public async Task<AdminPostDto> Handle(UnhidePostCommand request, CancellationToken ct)
    {
        var post = await LoadAsync(request.Id, ct);
        post.Unhide();
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Publication rétablie", "Post", post.Id, $"« {Text.Excerpt(post.Content)} »", ct);
        return (await reader.DescribeAsync([post], ct))[0];
    }

    public async Task<bool> Handle(DeletePostCommand request, CancellationToken ct)
    {
        var post = await LoadAsync(request.Id, ct);
        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Publication supprimée", "Post", post.Id, $"« {Text.Excerpt(post.Content)} »", ct);
        return true;
    }

    public async Task<bool> Handle(DeleteCommentCommand request, CancellationToken ct)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == request.Id, ct) ?? throw new NotFoundException("Commentaire");
        (await db.Posts.FirstOrDefaultAsync(p => p.Id == comment.PostId, ct))?.CommentRemoved();
        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);
        await admin.LogAsync("Commentaire supprimé", "Comment", comment.Id, $"« {Text.Excerpt(comment.Content)} »", ct);
        return true;
    }

    private async Task<Post> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Posts.Include(p => p.Likes).FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Publication");
}

/// <summary>Communauté supprimée : ses publications restent, hors communauté.</summary>
public sealed class DetachPostsFromDeletedCommunity(IFeedDb db) : IIntegrationEventHandler<CommunityDeleted>
{
    public async Task Handle(CommunityDeleted e, CancellationToken ct)
    {
        foreach (var post in await db.Posts.Where(p => p.CommunityId == e.CommunityId).ToListAsync(ct))
        {
            post.DetachFromCommunity();
        }

        await db.SaveChangesAsync(ct);
    }
}
