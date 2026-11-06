using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.MockData.Repositories;

public sealed class MockCommentRepository(MockDataStore store, IMessenger messenger, DeveloperOptions developer, TimeProvider clock)
    : MockRepositoryBase(store, messenger, developer, clock), ICommentRepository
{
    public const int MaxLength = 1000;

    public async Task<IReadOnlyList<Comment>> GetForPostAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);
        lock (Store.SyncRoot)
        {
            return Store.Comments.Where(c => c.PostId == postId).OrderBy(c => c.CreatedAt).ToList();
        }
    }

    public async Task<Comment> AddAsync(CreateCommentRequest request, CancellationToken cancellationToken = default)
    {
        await SimulateNetworkAsync(cancellationToken);

        var content = request.Content.Trim();
        if (content.Length == 0)
        {
            throw new DomainException("Le commentaire est vide.");
        }

        if (content.Length > MaxLength)
        {
            throw new DomainException($"Le commentaire ne doit pas dépasser {MaxLength} caractères.");
        }

        var comment = new Comment
        {
            Id = NewId(),
            PostId = request.PostId,
            AuthorId = request.AuthorId,
            Content = content,
            CreatedAt = Now
        };

        Post updatedPost;
        lock (Store.SyncRoot)
        {
            updatedPost = MockDataStore.Replace(
                Store.Posts,
                p => p.Id == request.PostId,
                p => p with { CommentCount = p.CommentCount + 1 },
                "Publication");
            Store.Comments.Add(comment);
        }

        Messenger.Send(new CommentAddedMessage(comment));
        Messenger.Send(new PostChangedMessage(updatedPost));
        return comment;
    }
}
