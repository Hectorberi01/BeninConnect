using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Profile.ViewModels;
using BeninConnect.App.Shared.Contracts;
using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Feed.ViewModels;

public sealed partial class CommentItemViewModel(Comment comment, User author, DateTimeOffset now, Action<Guid> openAuthor) : ObservableObject
{
    public Guid Id => comment.Id;
    public string AuthorName => author.DisplayName;
    public string AuthorInitials => author.Initials;
    public string AuthorColor => author.AvatarColor;
    public string Content => comment.Content;
    public string TimeText => TimeFormatter.Relative(comment.CreatedAt, now);

    [RelayCommand]
    private void OpenAuthor() => openAuthor(author.Id);
}

/// <summary>Détail d'une publication et ses commentaires (écrans « Détail » et « Commentaires »).</summary>
public sealed partial class PostDetailsViewModel(
    INavigationService navigation,
    IPostRepository posts,
    ICommentRepository comments,
    IUserRepository users,
    PostItemFactory postFactory,
    ISessionService session,
    TimeProvider clock) : PageViewModel(navigation)
{
    private Guid _postId;

    [ObservableProperty]
    private PostItemViewModel? _post;

    public ObservableCollection<CommentItemViewModel> Comments { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommentCommand))]
    private string _newComment = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommentCommand))]
    private bool _isSending;

    [ObservableProperty]
    private string? _commentError;

    [ObservableProperty]
    private bool _focusComment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoComments))]
    private bool _hasComments;

    public bool HasNoComments => !HasComments;

    public string MyInitials => session.CurrentUser?.Initials ?? string.Empty;
    public string MyColor => session.CurrentUser?.AvatarColor ?? "#087F5B";

    public override Task OnNavigatedToAsync(object? parameter)
    {
        switch (parameter)
        {
            case Guid id:
                _postId = id;
                break;
            case PostDetailsParameter p:
                _postId = p.PostId;
                FocusComment = p.FocusComment;
                break;
        }

        return ReloadAsync();
    }

    protected override Task ReloadAsync() => LoadAsync(async ct =>
    {
        var post = await posts.GetByIdAsync(_postId, ct) ?? throw new NotFoundException("Publication");
        var items = await postFactory.CreateAsync([post], ct);
        Post = items.FirstOrDefault() ?? throw new NotFoundException("Auteur");

        var list = await comments.GetForPostAsync(_postId, ct);
        var authors = await users.GetByIdsAsync(list.Select(c => c.AuthorId).Distinct(), ct);
        var now = clock.GetLocalNow();

        Comments.Clear();
        foreach (var c in list.Where(c => authors.ContainsKey(c.AuthorId)))
        {
            Comments.Add(new CommentItemViewModel(c, authors[c.AuthorId], now, OpenAuthor));
        }

        HasComments = Comments.Count > 0;
        return true;
    });

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(NewComment);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendCommentAsync()
    {
        CommentError = null;
        if (session.CurrentUser is not { } me)
        {
            return;
        }

        IsSending = true;
        try
        {
            var comment = await comments.AddAsync(new CreateCommentRequest(_postId, me.Id, NewComment));
            Comments.Add(new CommentItemViewModel(comment, me, clock.GetLocalNow(), OpenAuthor));
            HasComments = true;
            NewComment = string.Empty;
        }
        catch (Exception ex)
        {
            CommentError = ErrorMessages.From(ex);
        }
        finally
        {
            IsSending = false;
        }
    }

    private void OpenAuthor(Guid userId) => Navigation.NavigateTo<ProfileViewModel>(userId);
}
