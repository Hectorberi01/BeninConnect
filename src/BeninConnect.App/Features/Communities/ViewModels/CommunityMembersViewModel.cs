using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Profile.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BeninConnect.App.Features.Communities.ViewModels;

public sealed partial class CommunityMembersViewModel(INavigationService navigation, ICommunityRepository communities) : PageViewModel(navigation)
{
    private readonly List<UserItemViewModel> _all = [];
    private Guid _communityId;

    public ObservableCollection<UserItemViewModel> Members { get; } = [];

    [ObservableProperty] private string _title = "Membres";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _countText = string.Empty;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        _communityId = parameter is Guid id ? id : Guid.Empty;
        return ReloadAsync();
    }

    protected override Task ReloadAsync() => LoadAsync(async ct =>
    {
        var community = await communities.GetByIdAsync(_communityId, ct);
        Title = community is null ? "Membres" : $"Membres · {community.Name}";

        var members = await communities.GetMembersAsync(_communityId, ct);
        _all.Clear();
        _all.AddRange(members.Select(m => new UserItemViewModel(
            m.User,
            string.IsNullOrEmpty(m.User.Profession) ? m.User.Location : $"{m.User.Profession} · {m.User.Location}",
            u => Navigation.NavigateTo<ProfileViewModel>(u.Id),
            m.Role switch
            {
                CommunityRole.Owner => "Créateur",
                CommunityRole.Moderator => "Modérateur",
                _ => null
            })));

        CountText = $"{_all.Count} membre{(_all.Count > 1 ? "s" : "")}";
        ApplyFilter();
        return _all.Count > 0;
    });

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        if (State is ViewState.Content or ViewState.Empty)
        {
            State = Members.Count > 0 ? ViewState.Content : ViewState.Empty;
        }
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        Members.Clear();
        foreach (var m in _all.Where(m => q.Length == 0
                                          || m.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                          || m.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            Members.Add(m);
        }
    }
}
