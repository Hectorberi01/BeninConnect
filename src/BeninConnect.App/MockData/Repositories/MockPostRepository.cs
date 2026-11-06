using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.MockData.Repositories;

public sealed class MockPostRepository(MockDataStore store, IMessenger messenger, DeveloperOptions developer, TimeProvider clock)
    : MockRepositoryBase(store, messenger, developer, clock), IPostRepository
{
    public async Task<PagedResult<Post>> GetFeedAsync(Guid viewerId, int skip, int take, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            var visible = VisibleTo(viewerId).OrderByDescending(p => p.CreatedAt).ToList();
            return new PagedResult<Post>(visible.Skip(skip).Take(take).ToList(), visible.Count, skip);
        }
    }

    public async Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Posts.FirstOrDefault(p => p.Id == id);
        }
    }

    public async Task<IReadOnlyList<Post>> GetByAuthorAsync(Guid authorId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Posts.Where(p => p.AuthorId == authorId).OrderByDescending(p => p.CreatedAt).ToList();
        }
    }

    public async Task<IReadOnlyList<Post>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Posts.Where(p => p.CommunityId == communityId).OrderByDescending(p => p.CreatedAt).ToList();
        }
    }

    public async Task<IReadOnlyList<Post>> SearchAsync(string query, Guid viewerId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        var q = query.Trim();
        if (q.Length == 0)
        {
            return [];
        }

        lock (Store.SyncRoot)
        {
            return VisibleTo(viewerId)
                .Where(p => Matches(p.Content, q))
                .OrderByDescending(p => p.CreatedAt)
                .Take(30)
                .ToList();
        }
    }

    public async Task<Post> CreateAsync(CreatePostRequest request, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new DomainException("Le contenu de la publication est vide.");
        }

        var post = new Post
        {
            Id = NewId(),
            AuthorId = request.AuthorId,
            CommunityId = request.CommunityId,
            Content = request.Content.Trim(),
            ImagePath = request.ImagePath,
            Visibility = request.Visibility,
            CreatedAt = Now
        };

        lock (Store.SyncRoot)
        {
            Store.Posts.Add(post);
        }

        Messenger.Send(new PostCreatedMessage(post));
        return post;
    }

    public async Task ToggleLikeAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        Post updated;
        lock (Store.SyncRoot)
        {
            updated = MockDataStore.Replace(Store.Posts, p => p.Id == postId, p =>
            {
                var likes = new HashSet<Guid>(p.LikedBy);
                if (!likes.Add(userId))
                {
                    likes.Remove(userId);
                }

                return p with { LikedBy = likes };
            }, "Publication");
        }

        Messenger.Send(new PostChangedMessage(updated));
    }

    public async Task ShareAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        Post updated;
        lock (Store.SyncRoot)
        {
            updated = MockDataStore.Replace(Store.Posts, p => p.Id == postId, p => p with { ShareCount = p.ShareCount + 1 }, "Publication");
        }

        Messenger.Send(new PostChangedMessage(updated));
    }

    // Appelé sous verrou.
    private IEnumerable<Post> VisibleTo(Guid viewerId)
    {
        var memberOf = Store.Communities.Where(c => c.MemberIds.Contains(viewerId)).Select(c => c.Id).ToHashSet();
        return Store.Posts.Where(p =>
            p.AuthorId == viewerId
            || p.Visibility == PostVisibility.Public
            || (p.Visibility == PostVisibility.CommunityMembers && p.CommunityId is { } cid && memberOf.Contains(cid)));
    }
}
