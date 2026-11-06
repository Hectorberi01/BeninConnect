using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Messaging.Contracts;
using BeninConnect.App.Features.Profile.ViewModels;
using BeninConnect.App.Shared.Contracts;
using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BeninConnect.App.Features.Messaging.ViewModels;

/// <summary>Choix d'un membre pour démarrer une conversation.</summary>
public sealed partial class NewConversationViewModel(
    INavigationService navigation,
    IUserRepository users,
    IConversationRepository conversations,
    ISessionService session,
    IToastService toast) : PageViewModel(navigation)
{
    private readonly List<UserItemViewModel> _all = [];

    public ObservableCollection<UserItemViewModel> People { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    public override Task OnNavigatedToAsync(object? parameter) => ReloadAsync();

    protected override Task ReloadAsync() => LoadAsync(async ct =>
    {
        var me = session.CurrentUserId;
        var list = await users.GetAllAsync(ct);
        _all.Clear();
        _all.AddRange(list.Where(u => u.Id != me).Select(u => new UserItemViewModel(u, $"{u.Profession} · {u.City}", Start)));
        ApplyFilter();
        return _all.Count > 0;
    });

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        People.Clear();
        foreach (var p in _all.Where(p => q.Length == 0
                                          || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                          || p.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            People.Add(p);
        }
    }

    private async void Start(User user)
    {
        // async void limité à ce gestionnaire d'interface ; toutes les erreurs sont capturées.
        try
        {
            var conversation = await conversations.GetOrCreateDirectAsync(session.CurrentUserId, user.Id);
            Navigation.ReplaceWith<ConversationViewModel>(conversation.Id);
        }
        catch (Exception ex)
        {
            toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
    }
}
