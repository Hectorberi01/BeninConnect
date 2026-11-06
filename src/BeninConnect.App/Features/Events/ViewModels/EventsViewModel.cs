using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Events.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Features.Events.ViewModels;

public sealed record PeriodOption(EventPeriod Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Liste des événements à venir : recherche, catégories, ville et période.</summary>
public sealed partial class EventsViewModel : PageViewModel
{
    public const string AllCities = "Toutes les villes";
    private const string AllCategories = "*";

    private readonly IEventRepository _events;
    private readonly EventItemFactory _factory;
    private bool _initialized;

    public EventsViewModel(INavigationService navigation, IEventRepository events, EventItemFactory factory, IMessenger messenger)
        : base(navigation)
    {
        _events = events;
        _factory = factory;
        messenger.Register<EventsViewModel, EventCreatedMessage>(this, static (r, m) => _ = r.ReloadAsync());
    }

    public ObservableCollection<Chip> Categories { get; } = [];
    public ObservableCollection<string> Cities { get; } = [];
    public ObservableCollection<EventItemViewModel> Events { get; } = [];

    public IReadOnlyList<PeriodOption> Periods { get; } =
    [
        new(EventPeriod.All, "Toutes les dates"),
        new(EventPeriod.ThisWeek, "Cette semaine"),
        new(EventPeriod.ThisMonth, "Ce mois-ci"),
        new(EventPeriod.Later, "Plus tard"),
    ];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedCity = AllCities;
    [ObservableProperty] private PeriodOption? _selectedPeriod;
    [ObservableProperty] private string _selectedCategory = AllCategories;
    [ObservableProperty] private string _resultText = string.Empty;

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        SelectedPeriod = Periods[0];

        var categories = await _events.GetCategoriesAsync();
        Categories.Add(new Chip(AllCategories, "Tous", OnCategorySelected, isSelected: true));
        foreach (var c in categories)
        {
            Categories.Add(new Chip(c, c, OnCategorySelected));
        }

        Cities.Add(AllCities);
        foreach (var city in await _events.GetCitiesAsync())
        {
            Cities.Add(city);
        }

        _initialized = true;
        await ReloadAsync();
    }

    protected override Task ReloadAsync() => LoadAsync(async ct =>
    {
        var filter = new EventFilter(
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            SelectedCategory == AllCategories ? null : SelectedCategory,
            SelectedCity == AllCities ? null : SelectedCity,
            SelectedPeriod?.Value ?? EventPeriod.All);

        var list = await _events.GetUpcomingAsync(filter, ct);
        Events.Clear();
        foreach (var e in _factory.Create(list))
        {
            Events.Add(e);
        }

        ResultText = list.Count switch
        {
            0 => "Aucun événement",
            1 => "1 événement à venir",
            var n => $"{n} événements à venir"
        };
        return Events.Count > 0;
    }, showLoading: Events.Count == 0);

    partial void OnSearchTextChanged(string value) => Refilter();
    partial void OnSelectedCityChanged(string value) => Refilter();
    partial void OnSelectedPeriodChanged(PeriodOption? value) => Refilter();

    private void Refilter()
    {
        if (_initialized)
        {
            _ = ReloadAsync();
        }
    }

    private void OnCategorySelected(Chip chip)
    {
        Chip.SelectExclusive(Categories, chip);
        SelectedCategory = chip.Key;
        Refilter();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        _initialized = false;
        SearchText = string.Empty;
        SelectedCity = AllCities;
        SelectedPeriod = Periods[0];
        if (Categories.FirstOrDefault() is { } all)
        {
            Chip.SelectExclusive(Categories, all);
            SelectedCategory = all.Key;
        }

        _initialized = true;
        _ = ReloadAsync();
    }

    [RelayCommand]
    private void CreateEvent() => Navigation.NavigateTo<CreateEventViewModel>();
}
