using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Media.Contracts;
using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Feed.Application;

/// <summary>Suppression d'une publication par son auteur (commentaires et j'aime inclus).</summary>
public sealed record DeleteMyPostCommand(Guid Id) : ICommand<bool>;

public sealed class DeleteMyPostHandler(IFeedDb db, IEventBus bus, ICurrentUser me) : IRequestHandler<DeleteMyPostCommand, bool>
{
    public async Task<bool> Handle(DeleteMyPostCommand request, CancellationToken ct)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == request.Id, ct) ?? throw new NotFoundException("Publication");
        if (post.AuthorId != me.Id)
        {
            throw new ForbiddenException("Vous ne pouvez supprimer que vos propres publications.");
        }

        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);
        if (post.ImagePath is { } image)
        {
            await bus.PublishAsync(new ImagesReleased([image]), ct);
        }

        return true;
    }
}

/// <summary>Compte supprimé : publications, commentaires, j'aime et réactions du membre effacés.</summary>
public sealed class DeleteMemberFeedData(IFeedDb db, IEventBus bus, TimeProvider clock) : IIntegrationEventHandler<MemberDeleted>
{
    public async Task Handle(MemberDeleted e, CancellationToken ct)
    {
        var id = e.UserId;
        var now = clock.GetUtcNow();

        var posts = await db.Posts.Where(p => p.AuthorId == id).ToListAsync(ct);
        var images = posts.Select(p => p.ImagePath).OfType<string>().ToList();
        var ownIds = posts.Select(p => p.Id).ToList();
        db.Posts.RemoveRange(posts);

        // Commentaires sur les publications des autres : compteurs mis à jour
        var comments = await db.Comments.Where(c => c.AuthorId == id && !ownIds.Contains(c.PostId)).ToListAsync(ct);
        var postIds = comments.Select(c => c.PostId).Distinct().ToList();
        var commented = await db.Posts.Where(p => postIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var comment in comments)
        {
            commented.GetValueOrDefault(comment.PostId)?.CommentRemoved();
        }

        db.Comments.RemoveRange(comments);

        foreach (var post in await db.Posts.Include(p => p.Likes).Where(p => !ownIds.Contains(p.Id) && p.Likes.Any(l => l.UserId == id)).ToListAsync(ct))
        {
            post.ToggleLike(id, now); // déjà aimé : retire le j'aime
        }

        foreach (var comment in await db.Comments.Include(c => c.Reactions).Where(c => c.AuthorId != id && !ownIds.Contains(c.PostId) && c.Reactions.Any(r => r.UserId == id)).ToListAsync(ct))
        {
            comment.React(id, comment.Reactions.First(r => r.UserId == id).Emoji, now); // même emoji : retire la réaction
        }

        await db.SaveChangesAsync(ct);
        if (images.Count > 0)
        {
            await bus.PublishAsync(new ImagesReleased(images), ct);
        }
    }
}
