using BeninConnect.Modules.Feed.Application;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Feed.Infrastructure.Persistence;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Feed.Infrastructure;

public sealed class FeedModuleService(FeedDbContext db, AdminPostReader reader) : IFeedModule, IModuleStatistics
{
    public async Task<IReadOnlyDictionary<Guid, ContentPreview>> GetPostPreviewsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Posts.AsNoTracking().Where(p => list.Contains(p.Id))
            .Select(p => new ContentPreview(p.Id, p.Content, p.AuthorId, p.Id)).ToDictionaryAsync(p => p.Id, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, ContentPreview>> GetCommentPreviewsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Comments.AsNoTracking().Where(c => list.Contains(c.Id))
            .Select(c => new ContentPreview(c.Id, c.Content, c.AuthorId, c.PostId)).ToDictionaryAsync(c => c.Id, ct);
    }

    public async Task HidePostAsync(Guid postId, string reason, CancellationToken ct)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct) ?? throw new NotFoundException("Publication");
        post.Hide(reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeletePostAsync(Guid postId, CancellationToken ct)
    {
        db.Posts.Remove(await db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct) ?? throw new NotFoundException("Publication"));
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCommentAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct) ?? throw new NotFoundException("Commentaire");
        (await db.Posts.FirstOrDefaultAsync(p => p.Id == comment.PostId, ct))?.CommentRemoved();
        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountPostsByAuthorAsync(IReadOnlyCollection<Guid>? authorIds, CancellationToken ct)
    {
        var posts = db.Posts.AsNoTracking();
        if (authorIds is not null)
        {
            posts = posts.Where(p => authorIds.Contains(p.AuthorId));
        }

        return await posts.GroupBy(p => p.AuthorId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountCommentsByAuthorAsync(IReadOnlyCollection<Guid>? authorIds, CancellationToken ct)
    {
        var comments = db.Comments.AsNoTracking();
        if (authorIds is not null)
        {
            comments = comments.Where(c => authorIds.Contains(c.AuthorId));
        }

        return await comments.GroupBy(c => c.AuthorId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountPostsByCommunityAsync(CancellationToken ct) =>
        await db.Posts.AsNoTracking().Where(p => p.CommunityId != null).GroupBy(p => p.CommunityId!.Value)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<IReadOnlyList<AdminPostDto>> GetRecentPostsForAdminAsync(Guid authorId, int take, CancellationToken ct)
    {
        var posts = await db.Posts.AsNoTracking().Include(p => p.Likes).Where(p => p.AuthorId == authorId)
            .OrderByDescending(p => p.CreatedAt).Take(take).ToListAsync(ct);
        return await reader.DescribeAsync(posts, ct);
    }

    public async Task<FeedStats> GetStatsAsync(DateTimeOffset since, CancellationToken ct) => new(
        await db.Posts.CountAsync(ct),
        await db.Posts.CountAsync(p => p.IsHidden, ct),
        await db.Comments.CountAsync(ct),
        await db.Posts.AsNoTracking().Where(p => p.CreatedAt >= since).Select(p => p.CreatedAt).ToListAsync(ct));

    public async Task<IReadOnlyList<DateTimeOffset>> GetCommentDatesAsync(DateTimeOffset since, CancellationToken ct) =>
        await db.Comments.AsNoTracking().Where(c => c.CreatedAt >= since).Select(c => c.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct) =>
    [
        new("Publications", await db.Posts.CountAsync(ct)),
        new("Commentaires", await db.Comments.CountAsync(ct))
    ];
}
