using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Events.Models;
using BeninConnect.App.Features.Events.ViewModels;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Feed.ViewModels;
using BeninConnect.App.Features.Notifications.Contracts;
using BeninConnect.App.Features.Notifications.Models;
using BeninConnect.App.Features.Notifications.ViewModels;
using BeninConnect.App.Features.Search.ViewModels;
using BeninConnect.App.Shared.Models;
using BeninConnect.App.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Features.Home.ViewModels;

/// <summary>Accueil : création rapide, suggestions de communautés, fil paginé, événements recommandés.</summary>
public sealed partial class HomeViewModel : ViewModelBase
{
    public const int PageSize = 10;

    private readonly IPostRepository _posts;
    private readonly ICommunityRepository _communities;
    private readonly IEventRepository _events;
    private readonly INotificationRepository _notifications;
    private readonly PostItemFactory _postFactory;
    private readonly CommunityItemFactory _communityFactory;
    private readonly EventItemFactory _eventFactory;
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IToastService _toast;
    private readonly IMessenger _messenger;
    private int _loadedCount;

    public HomeViewModel(
        IPostRepository posts,
        ICommunityRepository communities,
        IEventRepository events,
        INotificationRepository notifications,
        PostItemFactory postFactory,
        CommunityItemFactory communityFactory,
        EventItemFactory eventFactory,
        INavigationService navigation,
        ISessionService session,
        IToastService toast,
        IMessenger messenger)
    {
        _posts = posts;
        _communities = communities;
        _events = events;
        _notifications = notifications;
        _postFactory = postFactory;
        _communityFactory = communityFactory;
        _eventFactory = eventFactory;
        _navigation = navigation;
        _session = session;
        _toast = toast;
        _messenger = messenger;

        messenger.Register<HomeViewModel, PostCreatedMessage>(this, static (r, m) => _ = r.OnPostCreatedAsync(m.Post));
        messenger.Register<HomeViewModel, NotificationsChangedMessage>(this, static (r, m) => _ = r.RefreshNotificationsAsync());
        messenger.Register<HomeViewModel, UserUpdatedMessage>(this, static (r, m) => r.RefreshUser());
        messenger.Register<HomeViewModel, MembershipChangedMessage>(this, static (r, m) => r.OnMembershipChanged(m));
    }

    public ObservableCollection<PostItemViewModel> Posts { get; } = [];
    public ObservableCollection<CommunityItemViewModel> Suggestions { get; } = [];
    public ObservableCollection<EventItemViewModel> Events { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    private bool _hasMore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    private bool _isLoadingMore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadNotifications))]
    private int _unreadNotifications;

    [ObservableProperty]
    private string _greeting = "Bonjour";

    [ObservableProperty]
    private string _userInitials = string.Empty;

    [ObservableProperty]
    private string _userColor = "#087F5B";

    [ObservableProperty]
    private string _composerPrompt = "Quoi de neuf ?";

    public bool HasUnreadNotifications => UnreadNotifications > 0;
    public bool ShowEndOfFeed => IsContent && !HasMore;
    public bool CanLoadMore => HasMore && !IsLoadingMore;
    public bool HasSuggestions => Suggestions.Count > 0;
    public bool HasEvents => Events.Count > 0;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(State) or nameof(HasMore))
        {
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(ShowEndOfFeed)));
        }
    }

    public override Task OnNavigatedToAsync(object? parameter)
    {
        RefreshUser();
        return ReloadAsync();
    }

    protected override Task ReloadAsync() => LoadAsync(async ct =>
    {
        var me = _session.CurrentUserId;
        var page = await _posts.GetFeedAsync(me, 0, PageSize, ct);
        var items = await _postFactory.CreateAsync(page.Items, ct);

        Posts.Clear();
        foreach (var item in items)
        {
            Posts.Add(item);
        }

        _loadedCount = page.Items.Count;
        HasMore = page.HasMore;

        await LoadSidebarAsync(me, ct);
        await RefreshNotificationsAsync();
        return Posts.Count > 0;
    });

    private async Task LoadSidebarAsync(Guid me, CancellationToken ct)
    {
        var all = await _communities.GetAllAsync(ct);
        var suggestions = await _communityFactory.CreateAsync(all.Where(c => !c.MemberIds.Contains(me)).Take(8), ct);
        Suggestions.Clear();
        foreach (var s in suggestions)
        {
            Suggestions.Add(s);
        }

        var upcoming = await _events.GetUpcomingAsync(new EventFilter(), ct);
        Events.Clear();
        foreach (var e in _eventFactory.Create(upcoming.Take(5)))
        {
            Events.Add(e);
        }

        OnPropertyChanged(nameof(HasSuggestions));
        OnPropertyChanged(nameof(HasEvents));
    }

    /// <summary>Pagination simulée : charge la page suivante du fil.</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!CanLoadMore)
        {
            return;
        }

        IsLoadingMore = true;
        try
        {
            var page = await _posts.GetFeedAsync(_session.CurrentUserId, _loadedCount, PageSize);
            var known = Posts.Select(p => p.Id).ToHashSet();
            var items = await _postFactory.CreateAsync(page.Items.Where(p => !known.Contains(p.Id)).ToList());
            foreach (var item in items)
            {
                Posts.Add(item);
            }

            _loadedCount += page.Items.Count;
            HasMore = page.HasMore;
        }
        catch (Exception ex)
        {
            _toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync();

    [RelayCommand]
    private void OpenSearch() => _navigation.NavigateTo<SearchViewModel>();

    [RelayCommand]
    private void OpenNotifications() => _navigation.NavigateTo<NotificationsViewModel>();

    [RelayCommand]
    private void CreatePost() => _navigation.NavigateTo<CreatePostViewModel>();

    [RelayCommand]
    private void CreatePostWithImage() => _navigation.NavigateTo<CreatePostViewModel>(new CreatePostParameter(OpenImagePicker: true));

    [RelayCommand]
    private void OpenEvents() => _navigation.NavigateTo<EventsViewModel>();

    [RelayCommand]
    private void OpenProfile() => _messenger.Send(new SelectTabMessage(ShellTab.Profile));

    [RelayCommand]
    private void OpenCommunities() => _messenger.Send(new SelectTabMessage(ShellTab.Communities));

    private async Task OnPostCreatedAsync(Post post)
    {
        var items = await _postFactory.CreateAsync([post]);
        if (items.Count > 0)
        {
            Posts.Insert(0, items[0]);
            _loadedCount++;
            State = ViewState.Content;
        }
    }

    private void OnMembershipChanged(MembershipChangedMessage message)
    {
        if (message.Status is MembershipStatus.Member or MembershipStatus.Owner)
        {
            var joined = Suggestions.FirstOrDefault(s => s.Id == message.CommunityId);
            if (joined is not null)
            {
                Suggestions.Remove(joined);
                OnPropertyChanged(nameof(HasSuggestions));
            }
        }
    }

    private async Task RefreshNotificationsAsync()
    {
        if (_session.CurrentUser is not { } me)
        {
            return;
        }

        try
        {
            UnreadNotifications = await _notifications.GetUnreadCountAsync(me.Id);
        }
        catch (Exception)
        {
            // Badge non critique.
        }
    }

    private void RefreshUser()
    {
        if (_session.CurrentUser is not { } me)
        {
            return;
        }

        Greeting = $"Bonjour {me.FirstName}";
        UserInitials = me.Initials;
        UserColor = me.AvatarColor;
        ComposerPrompt = $"Quoi de neuf, {me.FirstName} ?";
    }
}
