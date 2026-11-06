using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Profile.ViewModels;
using BeninConnect.App.Shared.Contracts;
using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Features.Feed.ViewModels;

/// <summary>Construit les cartes de publication (auteur + communauté résolus via les repositories).</summary>
public sealed class PostItemFactory(
    IUserRepository users,
    ICommunityRepository communities,
    IPostRepository posts,
    INavigationService navigation,
    IToastService toast,
    ISessionService session,
    IMessenger messenger,
    TimeProvider clock)
{
    internal IPostRepository Posts => posts;
    internal INavigationService Navigation => navigation;
    internal IToastService Toast => toast;
    internal ISessionService Session => session;
    internal IMessenger Messenger => messenger;
    internal TimeProvider Clock => clock;

    public async Task<IReadOnlyList<PostItemViewModel>> CreateAsync(IReadOnlyList<Post> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var authors = await users.GetByIdsAsync(items.Select(p => p.AuthorId).Distinct(), cancellationToken);
        var communityIds = items.Where(p => p.CommunityId.HasValue).Select(p => p.CommunityId!.Value).Distinct();
        var communityMap = await communities.GetByIdsAsync(communityIds, cancellationToken);

        return items
            .Where(p => authors.ContainsKey(p.AuthorId))
            .Select(p => new PostItemViewModel(
                p,
                authors[p.AuthorId],
                p.CommunityId is { } id && communityMap.TryGetValue(id, out var c) ? c : null,
                this))
            .ToList();
    }
}

/// <summary>Carte de publication : J'aime, Commenter, Partager, ouverture de l'auteur et de la communauté.</summary>
public sealed partial class PostItemViewModel : ObservableObject
{
    private readonly PostItemFactory _ctx;
    private Post _post;

    internal PostItemViewModel(Post post, User author, Community? community, PostItemFactory context)
    {
        _post = post;
        _ctx = context;
        Author = author;
        CommunityId = community?.Id;
        CommunityName = community?.Name;
        Apply(post);

        context.Messenger.Register<PostItemViewModel, PostChangedMessage>(this, static (r, m) =>
        {
            if (m.Post.Id == r.Id)
            {
                r.Apply(m.Post);
            }
        });
    }

    public Guid Id => _post.Id;
    public User Author { get; }
    public string AuthorName => Author.DisplayName;
    public string AuthorInitials => Author.Initials;
    public string AuthorColor => Author.AvatarColor;
    public Guid? CommunityId { get; }
    public string? CommunityName { get; }
    public bool HasCommunity => CommunityName is not null;
    public string Content => _post.Content;
    public string? ImagePath => _post.ImagePath;
    public bool HasImage => !string.IsNullOrEmpty(_post.ImagePath);
    public DateTimeOffset CreatedAt => _post.CreatedAt;
    public bool IsCommunityOnly => _post.Visibility == PostVisibility.CommunityMembers;
    public string TimeText => TimeFormatter.Relative(_post.CreatedAt, _ctx.Clock.GetLocalNow());

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LikeLabel))]
    private int _likeCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommentLabel))]
    private int _commentCount;

    [ObservableProperty]
    private int _shareCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LikeLabel))]
    private bool _isLiked;

    public string LikeLabel => LikeCount == 0 ? "J'aime" : LikeCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string CommentLabel => CommentCount == 0 ? "Commenter" : CommentCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string StatsText =>
        $"{LikeCount} j'aime · {CommentCount} commentaire{(CommentCount > 1 ? "s" : "")} · {ShareCount} partage{(ShareCount > 1 ? "s" : "")}";

    public void Apply(Post post)
    {
        _post = post;
        LikeCount = post.LikeCount;
        CommentCount = post.CommentCount;
        ShareCount = post.ShareCount;
        IsLiked = _ctx.Session.CurrentUser is { } me && post.IsLikedBy(me.Id);
        OnPropertyChanged(nameof(StatsText));
    }

    [RelayCommand]
    private async Task ToggleLikeAsync()
    {
        if (_ctx.Session.CurrentUser is not { } me)
        {
            return;
        }

        // Mise à jour optimiste, annulée en cas d'échec.
        var wasLiked = IsLiked;
        IsLiked = !wasLiked;
        LikeCount += wasLiked ? -1 : 1;
        OnPropertyChanged(nameof(StatsText));

        try
        {
            await _ctx.Posts.ToggleLikeAsync(Id, me.Id);
        }
        catch (Exception ex)
        {
            IsLiked = wasLiked;
            LikeCount += wasLiked ? 1 : -1;
            OnPropertyChanged(nameof(StatsText));
            _ctx.Toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        if (_ctx.Session.CurrentUser is not { } me)
        {
            return;
        }

        try
        {
            await _ctx.Posts.ShareAsync(Id, me.Id);
            _ctx.Toast.Show("Publication partagée (simulation : aucun envoi réel).", ToastKind.Success);
        }
        catch (Exception ex)
        {
            _ctx.Toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
    }

    [RelayCommand]
    private void OpenDetails() => _ctx.Navigation.NavigateTo<PostDetailsViewModel>(Id);

    [RelayCommand]
    private void Comment() => _ctx.Navigation.NavigateTo<PostDetailsViewModel>(new PostDetailsParameter(Id, FocusComment: true));

    [RelayCommand]
    private void OpenAuthor() => _ctx.Navigation.NavigateTo<ProfileViewModel>(Author.Id);

    [RelayCommand]
    private void OpenCommunity()
    {
        if (CommunityId is { } id)
        {
            _ctx.Navigation.NavigateTo<CommunityDetailsViewModel>(id);
        }
    }
}

public sealed record PostDetailsParameter(Guid PostId, bool FocusComment = false);
