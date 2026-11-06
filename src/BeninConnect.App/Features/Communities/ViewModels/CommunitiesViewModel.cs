using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Features.Communities.ViewModels;

/// <summary>Onglet Communautés : mes communautés, recommandations, catégories et recherche.</summary>
public sealed partial class CommunitiesViewModel : ViewModelBase
{
    private const string AllCategories = "*";

    private readonly ICommunityRepository _communities;
    private readonly CommunityItemFactory _factory;
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly List<CommunityItemViewModel> _all = [];

    public CommunitiesViewModel(
        ICommunityRepository communities,
        CommunityItemFactory factory,
        INavigationService navigation,
        ISessionService session,
        IMessenger messenger)
    {
        _communities = communities;
        _factory = factory;
        _navigation = navigation;
        _session = session;

        messenger.Register<CommunitiesViewModel, MembershipChangedMessage>(this, static (r, m) => r.OnMembershipChanged(m));
        messenger.Register<CommunitiesViewModel, CommunityCreatedMessage>(this, static (r, m) => _ = r.OnCreatedAsync(m.Community));
    }

    public ObservableCollection<Chip> Categories { get; } = [];
    public ObservableCollection<CommunityItemViewModel> MyCommunities { get; } = [];
    public ObservableCollection<CommunityItemViewModel> Recommended { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = AllCategories;

    [ObservableProperty]
    private bool _hasMyCommunities;

    [ObservableProperty]
    private bool _hasRecommended;

    [ObservableProperty]
    private string _recommendedTitle = "Communautés recommandées";

    public override Task OnNavigatedToAsync(object? parameter) => ReloadAsync();

    protected override Task ReloadAsync() => LoadAsync(async ct =>
    {
        var categories = await _communities.GetCategoriesAsync(ct);
        Categories.Clear();
        Categories.Add(new Chip(AllCategories, "Toutes", OnCategorySelected, isSelected: true));
        foreach (var category in categories)
        {
            Categories.Add(new Chip(category, category, OnCategorySelected));
        }

        SelectedCategory = AllCategories;

        var list = await _communities.GetAllAsync(ct);
        _all.Clear();
        _all.AddRange(await _factory.CreateAsync(list, ct));
        ApplyFilter();
        return _all.Count > 0;
    });

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void OnCategorySelected(Chip chip)
    {
        Chip.SelectExclusive(Categories, chip);
        SelectedCategory = chip.Key;
        ApplyFilter();
    }

    /// <summary>Filtrage local (recherche texte + catégorie) sur les données déjà chargées.</summary>
    public void ApplyFilter()
    {
        var query = SearchText.Trim();
        bool Matches(CommunityItemViewModel c) =>
            (SelectedCategory == AllCategories || c.Category == SelectedCategory)
            && (query.Length == 0
                || c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || c.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || c.Location.Contains(query, StringComparison.OrdinalIgnoreCase));

        var filtered = _all.Where(Matches).ToList();

        Replace(MyCommunities, filtered.Where(c => c.IsMember));
        Replace(Recommended, filtered.Where(c => !c.IsMember));

        HasMyCommunities = MyCommunities.Count > 0;
        HasRecommended = Recommended.Count > 0;
        RecommendedTitle = query.Length > 0 || SelectedCategory != AllCategories ? "Résultats" : "Communautés recommandées";

        if (State is ViewState.Content or ViewState.Empty)
        {
            State = filtered.Count > 0 ? ViewState.Content : ViewState.Empty;
        }
    }

    [RelayCommand]
    private void CreateCommunity() => _navigation.NavigateTo<CreateCommunityViewModel>();

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        if (Categories.FirstOrDefault() is { } all)
        {
            OnCategorySelected(all);
        }
    }

    private void OnMembershipChanged(MembershipChangedMessage message)
    {
        // L'ordre des destinataires du messenger n'est pas garanti : on met l'état à jour avant de répartir.
        if (_session.CurrentUser?.Id == message.UserId
            && _all.FirstOrDefault(c => c.Id == message.CommunityId) is { } item)
        {
            item.Status = message.Status;
        }

        ApplyFilter();
    }

    private async Task OnCreatedAsync(Community community)
    {
        var items = await _factory.CreateAsync([community]);
        _all.Insert(0, items[0]);
        ApplyFilter();
    }

    private static void Replace(ObservableCollection<CommunityItemViewModel> target, IEnumerable<CommunityItemViewModel> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
