using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Feed.ViewModels;
using BeninConnect.App.Features.Home.ViewModels;
using BeninConnect.App.Features.Messaging.Contracts;
using BeninConnect.App.Features.Messaging.Models;
using BeninConnect.App.Features.Messaging.ViewModels;
using BeninConnect.App.Features.Profile.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Shell;

public enum ShellTab
{
    Home,
    Communities,
    Messages,
    Profile
}

public sealed record SelectTabMessage(ShellTab Tab);

/// <summary>
/// Conteneur des 4 onglets persistants + bouton Publier. Les ViewModels d'onglet sont créés une fois
/// par session : changer d'onglet ne recharge rien.
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IConversationRepository _conversations;
    private readonly ISessionService _session;
    private readonly HashSet<ShellTab> _activated = [];

    public ShellViewModel(
        HomeViewModel home,
        CommunitiesViewModel communities,
        ConversationsViewModel messages,
        ProfileViewModel profile,
        INavigationService navigation,
        IConversationRepository conversations,
        ISessionService session,
        IMessenger messenger)
    {
        Home = home;
        Communities = communities;
        Messages = messages;
        Profile = profile;
        _navigation = navigation;
        _conversations = conversations;
        _session = session;

        messenger.Register<ShellViewModel, SelectTabMessage>(this, static (r, m) => r.Select(m.Tab));
        messenger.Register<ShellViewModel, ConversationChangedMessage>(this, static (r, m) => _ = r.RefreshUnreadAsync());
    }

    public HomeViewModel Home { get; }
    public CommunitiesViewModel Communities { get; }
    public ConversationsViewModel Messages { get; }
    public ProfileViewModel Profile { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSelected), nameof(IsCommunitiesSelected), nameof(IsMessagesSelected), nameof(IsProfileSelected))]
    private ShellTab _selectedTab = ShellTab.Home;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadMessages))]
    private int _unreadMessages;

    public bool IsHomeSelected => SelectedTab == ShellTab.Home;
    public bool IsCommunitiesSelected => SelectedTab == ShellTab.Communities;
    public bool IsMessagesSelected => SelectedTab == ShellTab.Messages;
    public bool IsProfileSelected => SelectedTab == ShellTab.Profile;
    public bool HasUnreadMessages => UnreadMessages > 0;

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        var tab = parameter as ShellTab? ?? ShellTab.Home;
        Select(tab);
        await RefreshUnreadAsync();
    }

    public override bool OnBackRequested()
    {
        if (SelectedTab == ShellTab.Home)
        {
            return false;
        }

        Select(ShellTab.Home);
        return true;
    }

    [RelayCommand]
    private void ShowHome() => Select(ShellTab.Home);

    [RelayCommand]
    private void ShowCommunities() => Select(ShellTab.Communities);

    [RelayCommand]
    private void ShowMessages() => Select(ShellTab.Messages);

    [RelayCommand]
    private void ShowProfile() => Select(ShellTab.Profile);

    /// <summary>Onglet « Publier » : ouvre l'écran de création.</summary>
    [RelayCommand]
    private void Publish() => _navigation.NavigateTo<CreatePostViewModel>();

    public void Select(ShellTab tab)
    {
        SelectedTab = tab;

        // Activation paresseuse : chaque onglet charge ses données à la première ouverture seulement.
        if (_activated.Add(tab))
        {
            ViewModelBase target = tab switch
            {
                ShellTab.Home => Home,
                ShellTab.Communities => Communities,
                ShellTab.Messages => Messages,
                _ => Profile
            };
            ActivationTask = target.OnNavigatedToAsync(null);
        }
    }

    /// <summary>Tâche d'activation du dernier onglet ouvert (attendue par les tests).</summary>
    public Task ActivationTask { get; private set; } = Task.CompletedTask;

    private async Task RefreshUnreadAsync()
    {
        if (_session.CurrentUser is not { } me)
        {
            return;
        }

        try
        {
            UnreadMessages = await _conversations.GetUnreadCountAsync(me.Id);
        }
        catch (Exception)
        {
            // Badge non critique.
        }
    }
}
