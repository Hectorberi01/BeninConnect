using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Events.ViewModels;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Feed.ViewModels;
using BeninConnect.App.Features.Profile.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Features.Communities.ViewModels;

public enum CommunitySection
{
    Posts,
    Members,
    Events
}

public sealed partial class CommunityDetailsViewModel : PageViewModel
{
    private readonly ICommunityRepository _communities;
    private readonly IPostRepository _posts;
    private readonly IEventRepository _events;
    private readonly CommunityItemFactory _communityFactory;
    private readonly PostItemFactory _postFactory;
    private readonly EventItemFactory _eventFactory;
    private Guid _communityId;

    public CommunityDetailsViewModel(
        INavigationService navigation,
        ICommunityRepository communities,
        IPostRepository posts,
        IEventRepository events,
        CommunityItemFactory communityFactory,
        PostItemFactory postFactory,
        EventItemFactory eventFactory,
        IMessenger messenger) : base(navigation)
    {
        _communities = communities;
        _posts = posts;
        _events = events;
        _communityFactory = communityFactory;
        _postFactory = postFactory;
        _eventFactory = eventFactory;

        messenger.Register<CommunityDetailsViewModel, MembershipChangedMessage>(this, static (r, m) =>
        {
            if (m.CommunityId == r._communityId)
            {
                _ = r.LoadContentAsync(showLoading: false);
            }
        });
        messenger.Register<CommunityDetailsViewModel, PostCreatedMessage>(this, static (r, m) =>
        {
            if (m.Post.CommunityId == r._communityId)
            {
                _ = r.LoadContentAsync(showLoading: false);
            }
        });
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRestricted), nameof(CanSeeContent))]
    private CommunityItemViewModel? _community;

    public ObservableCollection<PostItemViewModel> Posts { get; } = [];
    public ObservableCollection<UserItemViewModel> Members { get; } = [];
    public ObservableCollection<EventItemViewModel> Events { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPostsSection), nameof(IsMembersSection), nameof(IsEventsSection))]
    private CommunitySection _section = CommunitySection.Posts;

    [ObservableProperty] private bool _hasPosts;
    [ObservableProperty] private bool _hasEvents;
    [ObservableProperty] private int _totalMembers;

    public bool IsPostsSection => Section == CommunitySection.Posts;
    public bool IsMembersSection => Section == CommunitySection.Members;
    public bool IsEventsSection => Section == CommunitySection.Events;

    /// <summary>Communauté privée et utilisateur non membre : contenu masqué.</summary>
    public bool IsRestricted => Community is { IsPrivate: true, IsMember: false };
    public bool CanSeeContent => !IsRestricted;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        _communityId = parameter is Guid id ? id : Guid.Empty;
        return ReloadAsync();
    }

    protected override Task ReloadAsync() => LoadContentAsync(showLoading: true);

    private Task LoadContentAsync(bool showLoading) => LoadAsync(async ct =>
    {
        var community = await _communities.GetByIdAsync(_communityId, ct) ?? throw new NotFoundException("Communauté");
        var items = await _communityFactory.CreateAsync([community], ct);
        Community = items[0];

        Posts.Clear();
        Members.Clear();
        Events.Clear();

        if (!IsRestricted)
        {
            var posts = await _posts.GetByCommunityAsync(_communityId, ct);
            foreach (var post in await _postFactory.CreateAsync(posts, ct))
            {
                Posts.Add(post);
            }

            var members = await _communities.GetMembersAsync(_communityId, ct);
            TotalMembers = members.Count;
            foreach (var member in members.Take(8))
            {
                Members.Add(ToItem(member));
            }

            foreach (var e in _eventFactory.Create(await _events.GetForCommunityAsync(_communityId, ct)))
            {
                Events.Add(e);
            }
        }

        HasPosts = Posts.Count > 0;
        HasEvents = Events.Count > 0;
        OnPropertyChanged(nameof(IsRestricted));
        OnPropertyChanged(nameof(CanSeeContent));
        return true;
    }, showLoading);

    private UserItemViewModel ToItem(CommunityMember member) =>
        new(member.User,
            member.User.Location,
            u => Navigation.NavigateTo<ProfileViewModel>(u.Id),
            member.Role switch
            {
                CommunityRole.Owner => "Créateur",
                CommunityRole.Moderator => "Modérateur",
                _ => null
            });

    [RelayCommand]
    private void ShowPosts() => Section = CommunitySection.Posts;

    [RelayCommand]
    private void ShowMembers() => Section = CommunitySection.Members;

    [RelayCommand]
    private void ShowEvents() => Section = CommunitySection.Events;

    [RelayCommand]
    private void OpenAllMembers() => Navigation.NavigateTo<CommunityMembersViewModel>(_communityId);

    [RelayCommand]
    private void CreatePost() => Navigation.NavigateTo<CreatePostViewModel>(new CreatePostParameter(_communityId));

    [RelayCommand]
    private void CreateEvent() => Navigation.NavigateTo<CreateEventViewModel>(_communityId);
}
