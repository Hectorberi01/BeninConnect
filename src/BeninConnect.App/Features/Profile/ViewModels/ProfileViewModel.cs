using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Authentication.ViewModels;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Feed.ViewModels;
using BeninConnect.App.Features.Messaging.Contracts;
using BeninConnect.App.Features.Messaging.ViewModels;
using BeninConnect.App.Features.Settings.ViewModels;
using BeninConnect.App.Shared.Contracts;
using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Features.Profile.ViewModels;

/// <summary>
/// Profil. Sans paramètre : profil de l'utilisateur connecté (onglet). Avec un Guid : profil d'un autre membre (page).
/// </summary>
public sealed partial class ProfileViewModel : ViewModelBase
{
    public const int RecentPostCount = 10;

    private readonly IUserRepository _users;
    private readonly IPostRepository _posts;
    private readonly ICommunityRepository _communities;
    private readonly IConversationRepository _conversations;
    private readonly PostItemFactory _postFactory;
    private readonly CommunityItemFactory _communityFactory;
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IDialogService _dialogs;
    private readonly IPreferencesService _preferences;
    private readonly IToastService _toast;
    private readonly TimeProvider _clock;
    private Guid? _requestedUserId;

    public ProfileViewModel(
        IUserRepository users,
        IPostRepository posts,
        ICommunityRepository communities,
        IConversationRepository conversations,
        PostItemFactory postFactory,
        CommunityItemFactory communityFactory,
        INavigationService navigation,
        ISessionService session,
        IDialogService dialogs,
        IPreferencesService preferences,
        IToastService toast,
        TimeProvider clock,
        IMessenger messenger)
    {
        _users = users;
        _posts = posts;
        _communities = communities;
        _conversations = conversations;
        _postFactory = postFactory;
        _communityFactory = communityFactory;
        _navigation = navigation;
        _session = session;
        _dialogs = dialogs;
        _preferences = preferences;
        _toast = toast;
        _clock = clock;

        messenger.Register<ProfileViewModel, UserUpdatedMessage>(this, static (r, m) =>
        {
            if (m.User.Id == r.UserId)
            {
                r.ApplyUser(m.User);
            }
        });
        messenger.Register<ProfileViewModel, PostCreatedMessage>(this, static (r, m) =>
        {
            if (m.Post.AuthorId == r.UserId)
            {
                _ = r.LoadDetailsAsync(showLoading: false);
            }
        });
        messenger.Register<ProfileViewModel, MembershipChangedMessage>(this, static (r, m) =>
        {
            if (m.UserId == r.UserId)
            {
                _ = r.LoadDetailsAsync(showLoading: false);
            }
        });
    }

    public Guid UserId { get; private set; }

    public ObservableCollection<string> Interests { get; } = [];
    public ObservableCollection<CommunityItemViewModel> Communities { get; } = [];
    public ObservableCollection<PostItemViewModel> RecentPosts { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOtherUser))]
    private bool _isCurrentUser;

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _initials = string.Empty;
    [ObservableProperty] private string _avatarColor = "#087F5B";
    [ObservableProperty] private string? _coverImage;
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _profession = string.Empty;
    [ObservableProperty] private string _origin = string.Empty;
    [ObservableProperty] private string _bio = string.Empty;
    [ObservableProperty] private string _memberSince = string.Empty;
    [ObservableProperty] private int _postCount;
    [ObservableProperty] private int _communityCount;
    [ObservableProperty] private int _likesReceived;
    [ObservableProperty] private bool _hasPosts;
    [ObservableProperty] private bool _hasCommunities;
    [ObservableProperty] private bool _hasBio;

    public bool IsOtherUser => !IsCurrentUser;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        _requestedUserId = parameter as Guid?;
        return ReloadAsync();
    }

    protected override Task ReloadAsync() => LoadDetailsAsync(showLoading: true);

    private Task LoadDetailsAsync(bool showLoading) => LoadAsync(async ct =>
    {
        var me = _session.CurrentUserId;
        UserId = _requestedUserId ?? me;
        IsCurrentUser = UserId == me;

        var user = await _users.GetByIdAsync(UserId, ct) ?? throw new NotFoundException("Membre");
        ApplyUser(user);

        var posts = await _posts.GetByAuthorAsync(UserId, ct);
        var visible = IsCurrentUser ? posts : posts.Where(p => p.Visibility == PostVisibility.Public).ToList();
        PostCount = visible.Count;
        LikesReceived = visible.Sum(p => p.LikeCount);

        RecentPosts.Clear();
        foreach (var p in await _postFactory.CreateAsync(visible.Take(RecentPostCount).ToList(), ct))
        {
            RecentPosts.Add(p);
        }

        var communities = await _communities.GetForMemberAsync(UserId, ct);
        var shown = IsCurrentUser ? communities : communities.Where(c => c.Visibility == CommunityVisibility.Public).ToList();
        CommunityCount = shown.Count;
        Communities.Clear();
        foreach (var c in await _communityFactory.CreateAsync(shown, ct))
        {
            Communities.Add(c);
        }

        HasPosts = RecentPosts.Count > 0;
        HasCommunities = Communities.Count > 0;
        return true;
    }, showLoading);

    private void ApplyUser(User user)
    {
        DisplayName = user.DisplayName;
        Initials = user.Initials;
        AvatarColor = user.AvatarColor;
        CoverImage = user.CoverImage;
        Location = user.Location;
        Profession = user.Profession;
        Origin = string.IsNullOrEmpty(user.OriginCity) ? string.Empty : $"Originaire de {user.OriginCity}";
        Bio = user.Bio;
        HasBio = !string.IsNullOrWhiteSpace(user.Bio);
        MemberSince = $"Membre depuis {TimeFormatter.MonthYear(user.JoinedAt).ToLowerInvariant()}";

        Interests.Clear();
        foreach (var interest in user.Interests)
        {
            Interests.Add(interest);
        }
    }

    [RelayCommand]
    private void EditProfile() => _navigation.NavigateTo<EditProfileViewModel>();

    [RelayCommand]
    private void OpenSettings() => _navigation.NavigateTo<SettingsViewModel>();

    [RelayCommand]
    private void GoBack() => _navigation.GoBack();

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        try
        {
            var conversation = await _conversations.GetOrCreateDirectAsync(_session.CurrentUserId, UserId);
            _navigation.NavigateTo<ConversationViewModel>(conversation.Id);
        }
        catch (Exception ex)
        {
            _toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (!await _dialogs.ConfirmAsync("Se déconnecter ?", "Vous pourrez vous reconnecter avec le compte de démonstration.", "Se déconnecter", isDestructive: true))
        {
            return;
        }

        SignOut(_session, _preferences, _navigation);
    }

    /// <summary>Déconnexion partagée avec l'écran Paramètres.</summary>
    internal static void SignOut(ISessionService session, IPreferencesService preferences, INavigationService navigation)
    {
        var email = session.CurrentUser?.Email;
        preferences.Set(PreferenceKeys.RememberedAccount, null);
        session.SignOut();
        navigation.ResetTo<LoginViewModel>(email);
    }
}
