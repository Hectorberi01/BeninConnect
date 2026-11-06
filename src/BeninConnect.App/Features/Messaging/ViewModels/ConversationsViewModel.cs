using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Messaging.Contracts;
using BeninConnect.App.Features.Messaging.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Features.Messaging.ViewModels;

public sealed partial class ConversationItemViewModel(ConversationSummary summary, Guid me, DateTimeOffset now, Action<Guid> open) : ObservableObject
{
    public Guid Id => summary.Conversation.Id;
    public string Name => summary.OtherParticipant.DisplayName;
    public string Initials => summary.OtherParticipant.Initials;
    public string AvatarColor => summary.OtherParticipant.AvatarColor;
    public string City => summary.OtherParticipant.City;

    public string LastMessage => summary.LastMessage switch
    {
        null => "Nouvelle conversation",
        { } m when m.SenderId == me => $"Vous : {m.Content}",
        { } m => m.Content
    };

    public string TimeText => TimeFormatter.Chat(summary.LastMessage?.SentAt ?? summary.Conversation.CreatedAt, now);
    public int UnreadCount => summary.UnreadCount;
    public bool HasUnread => summary.UnreadCount > 0;

    [RelayCommand]
    private void Open() => open(Id);
}

/// <summary>Onglet Messages : liste des conversations, recherche, badge de non-lus.</summary>
public sealed partial class ConversationsViewModel : ViewModelBase
{
    private readonly IConversationRepository _conversations;
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly TimeProvider _clock;
    private readonly List<ConversationItemViewModel> _all = [];
    private bool _loaded;

    public ConversationsViewModel(
        IConversationRepository conversations,
        INavigationService navigation,
        ISessionService session,
        TimeProvider clock,
        IMessenger messenger)
    {
        _conversations = conversations;
        _navigation = navigation;
        _session = session;
        _clock = clock;

        messenger.Register<ConversationsViewModel, ConversationChangedMessage>(this, static (r, m) =>
        {
            if (r._loaded)
            {
                _ = r.RefreshAsync();
            }
        });
    }

    public ObservableCollection<ConversationItemViewModel> Conversations { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _unreadTotal;

    public override Task OnNavigatedToAsync(object? parameter) => ReloadAsync();

    protected override Task ReloadAsync() => LoadAsync(async ct =>
    {
        await FetchAsync(ct);
        _loaded = true;
        return _all.Count > 0;
    });

    /// <summary>Actualisation silencieuse (nouveau message, lecture).</summary>
    public Task RefreshAsync() => LoadAsync(async ct =>
    {
        await FetchAsync(ct);
        return _all.Count > 0;
    }, showLoading: false);

    private async Task FetchAsync(CancellationToken ct)
    {
        var me = _session.CurrentUserId;
        var summaries = await _conversations.GetSummariesAsync(me, ct);
        var now = _clock.GetLocalNow();
        _all.Clear();
        _all.AddRange(summaries.Select(s => new ConversationItemViewModel(s, me, now, OpenConversation)));
        UnreadTotal = summaries.Sum(s => s.UnreadCount);
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        Conversations.Clear();
        foreach (var c in _all.Where(c => q.Length == 0
                                          || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                          || c.LastMessage.Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            Conversations.Add(c);
        }
    }

    [RelayCommand]
    private void NewConversation() => _navigation.NavigateTo<NewConversationViewModel>();

    private void OpenConversation(Guid id) => _navigation.NavigateTo<ConversationViewModel>(id);
}
