using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Events.ViewModels;
using BeninConnect.App.Features.Feed.ViewModels;
using BeninConnect.App.Features.Profile.ViewModels;
using BeninConnect.App.Features.Search.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BeninConnect.App.Features.Search.ViewModels;

/// <summary>Recherche globale : membres, publications, communautés et événements, avec filtre par type.</summary>
public sealed partial class SearchViewModel(
    INavigationService navigation,
    ISearchService search,
    ISessionService session,
    PostItemFactory postFactory,
    CommunityItemFactory communityFactory,
    EventItemFactory eventFactory) : PageViewModel(navigation)
{
    public static readonly string[] Suggestions = ["Paris", "Montréal", "Étudiants", "Emploi", "Cuisine", "Tech", "Concert"];

    private SearchScope _scope = SearchScope.All;

    /// <summary>Délai avant lancement de la recherche pendant la saisie.</summary>
    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(300);

    public ObservableCollection<Chip> Scopes { get; } = [];
    public ObservableCollection<Chip> SuggestionChips { get; } = [];
    public ObservableCollection<UserItemViewModel> People { get; } = [];
    public ObservableCollection<PostItemViewModel> Posts { get; } = [];
    public ObservableCollection<CommunityItemViewModel> Communities { get; } = [];
    public ObservableCollection<EventItemViewModel> Events { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrompt))]
    private string _query = string.Empty;

    [ObservableProperty] private bool _hasPeople;
    [ObservableProperty] private bool _hasPosts;
    [ObservableProperty] private bool _hasCommunities;
    [ObservableProperty] private bool _hasEvents;
    [ObservableProperty] private string _summary = string.Empty;

    /// <summary>Aucune saisie : on affiche les suggestions.</summary>
    public bool IsPrompt => Query.Trim().Length < 2;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        foreach (var (scope, label) in new[]
                 {
                     (SearchScope.All, "Tout"),
                     (SearchScope.People, "Membres"),
                     (SearchScope.Posts, "Publications"),
                     (SearchScope.Communities, "Communautés"),
                     (SearchScope.Events, "Événements"),
                 })
        {
            Scopes.Add(new Chip(scope.ToString(), label, OnScopeSelected, scope == SearchScope.All));
        }

        foreach (var s in Suggestions)
        {
            SuggestionChips.Add(new Chip(s, s, chip => Query = chip.Label));
        }

        if (parameter is string initial)
        {
            Query = initial;
        }

        return Task.CompletedTask;
    }

    partial void OnQueryChanged(string value) => _ = SearchAsync(debounce: true);

    private void OnScopeSelected(Chip chip)
    {
        Chip.SelectExclusive(Scopes, chip);
        _scope = Enum.Parse<SearchScope>(chip.Key);
        _ = SearchAsync(debounce: false);
    }

    protected override Task ReloadAsync() => SearchAsync(debounce: false);

    public Task SearchAsync(bool debounce)
    {
        if (IsPrompt)
        {
            CancelPendingLoad();
            ClearResults();
            State = ViewState.Idle;
            return Task.CompletedTask;
        }

        return LoadAsync(async ct =>
        {
            if (debounce && Debounce > TimeSpan.Zero)
            {
                await Task.Delay(Debounce, ct);
            }

            var results = await search.SearchAsync(Query, _scope, session.CurrentUserId, ct);
            ClearResults();

            foreach (var u in results.People)
            {
                People.Add(new UserItemViewModel(u, $"{u.Profession} · {u.City}", user => Navigation.NavigateTo<ProfileViewModel>(user.Id)));
            }

            foreach (var p in await postFactory.CreateAsync(results.Posts, ct))
            {
                Posts.Add(p);
            }

            foreach (var c in await communityFactory.CreateAsync(results.Communities, ct))
            {
                Communities.Add(c);
            }

            foreach (var e in eventFactory.Create(results.Events))
            {
                Events.Add(e);
            }

            UpdateFlags();
            Summary = results.TotalCount switch
            {
                0 => string.Empty,
                1 => "1 résultat",
                var n => $"{n} résultats"
            };
            return results.TotalCount > 0;
        });
    }

    private void ClearResults()
    {
        People.Clear();
        Posts.Clear();
        Communities.Clear();
        Events.Clear();
        UpdateFlags();
    }

    private void UpdateFlags()
    {
        HasPeople = People.Count > 0;
        HasPosts = Posts.Count > 0;
        HasCommunities = Communities.Count > 0;
        HasEvents = Events.Count > 0;
    }
}
