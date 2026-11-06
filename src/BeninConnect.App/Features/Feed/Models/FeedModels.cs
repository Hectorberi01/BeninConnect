namespace BeninConnect.App.Features.Feed.Models;

public enum PostVisibility
{
    Public,
    CommunityMembers,
    OnlyMe
}

public sealed record Post
{
    public required Guid Id { get; init; }
    public required Guid AuthorId { get; init; }
    public Guid? CommunityId { get; init; }
    public required string Content { get; init; }
    public string? ImagePath { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public PostVisibility Visibility { get; init; } = PostVisibility.Public;
    public IReadOnlySet<Guid> LikedBy { get; init; } = new HashSet<Guid>();
    public int CommentCount { get; init; }
    public int ShareCount { get; init; }

    public int LikeCount => LikedBy.Count;

    public bool IsLikedBy(Guid userId) => LikedBy.Contains(userId);
}

public sealed record Comment
{
    public required Guid Id { get; init; }
    public required Guid PostId { get; init; }
    public required Guid AuthorId { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed record CreatePostRequest(
    Guid AuthorId,
    string Content,
    Guid? CommunityId,
    string? ImagePath,
    PostVisibility Visibility);

public sealed record CreateCommentRequest(Guid PostId, Guid AuthorId, string Content);

public sealed record PostChangedMessage(Post Post);

public sealed record PostCreatedMessage(Post Post);

public sealed record CommentAddedMessage(Comment Comment);
