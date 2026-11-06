using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Shared.Models;

namespace BeninConnect.App.Features.Feed.Contracts;

public interface IPostRepository
{
    /// <summary>Fil paginé visible par un utilisateur (publications publiques + communautés dont il est membre + les siennes).</summary>
    Task<PagedResult<Post>> GetFeedAsync(Guid viewerId, int skip, int take, CancellationToken cancellationToken = default);

    Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> GetByAuthorAsync(Guid authorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> SearchAsync(string query, Guid viewerId, CancellationToken cancellationToken = default);

    Task<Post> CreateAsync(CreatePostRequest request, CancellationToken cancellationToken = default);

    Task ToggleLikeAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default);

    Task ShareAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default);
}

public interface ICommentRepository
{
    Task<IReadOnlyList<Comment>> GetForPostAsync(Guid postId, CancellationToken cancellationToken = default);

    Task<Comment> AddAsync(CreateCommentRequest request, CancellationToken cancellationToken = default);
}
