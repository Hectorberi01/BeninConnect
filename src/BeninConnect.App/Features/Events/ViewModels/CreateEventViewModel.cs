using System.Collections.ObjectModel;
using System.Globalization;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Events.Models;
using BeninConnect.App.Features.Feed.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Events.ViewModels;

public sealed partial class CreateEventViewModel(
    INavigationService navigation,
    IEventRepository events,
    ICommunityRepository communities,
    ISessionService session,
    IToastService toast,
    TimeProvider clock) : PageViewModel(navigation)
{
    public ObservableCollection<string> Categories { get; } = [];
    public ObservableCollection<CommunityOption> CommunityOptions { get; } = [new(null, "Aucune")];
    public ObservableCollection<ImageOption> Images { get; } = [];

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string? _category;
    [ObservableProperty] private string _city = string.Empty;
    [ObservableProperty] private string _country = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private DateTimeOffset? _date;
    [ObservableProperty] private TimeSpan? _startTime = new TimeSpan(18, 0, 0);
    [ObservableProperty] private string _durationHours = "2";
    [ObservableProperty] private string _capacity = string.Empty;
    [ObservableProperty] private CommunityOption? _selectedCommunity;
    [ObservableProperty] private string? _imagePath;

    [ObservableProperty] private string? _titleError;
    [ObservableProperty] private string? _descriptionError;
    [ObservableProperty] private string? _categoryError;
    [ObservableProperty] private string? _cityError;
    [ObservableProperty] private string? _addressError;
    [ObservableProperty] private string? _dateError;
    [ObservableProperty] private string? _durationError;
    [ObservableProperty] private string? _capacityError;
    [ObservableProperty] private string? _formError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private bool _isSubmitting;

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        Date = clock.GetLocalNow().Date.AddDays(7);
        City = session.CurrentUser?.City ?? string.Empty;
        Country = session.CurrentUser?.Country ?? string.Empty;
        SelectedCommunity = CommunityOptions[0];

        for (var i = 1; i <= 10; i++)
        {
            Images.Add(new ImageOption($"Events/event_{i:00}.jpg", OnImageSelected));
        }

        OnImageSelected(Images[0]);

        try
        {
            foreach (var c in await events.GetCategoriesAsync())
            {
                Categories.Add(c);
            }

            foreach (var c in await communities.GetForMemberAsync(session.CurrentUserId))
            {
                CommunityOptions.Add(new CommunityOption(c.Id, c.Name));
            }

            if (parameter is Guid communityId && CommunityOptions.FirstOrDefault(o => o.Id == communityId) is { } preselected)
            {
                SelectedCommunity = preselected;
            }
        }
        catch (Exception ex)
        {
            FormError = ErrorMessages.From(ex);
        }
    }

    public DateTimeOffset? ComputeStart() =>
        Date is { } d && StartTime is { } t
            ? new DateTimeOffset(d.Date + t, clock.GetLocalNow().Offset)
            : null;

    public bool Validate()
    {
        TitleError = Validation.Length(Title, "Le titre", 5, 80);
        DescriptionError = Validation.Length(Description, "La description", 20, 1000);
        CategoryError = string.IsNullOrEmpty(Category) ? "Choisissez une catégorie." : null;
        CityError = Validation.Required(City, "La ville");
        AddressError = Validation.Required(Address, "L'adresse");

        var start = ComputeStart();
        DateError = start is null ? "Choisissez une date et une heure."
            : start <= clock.GetLocalNow() ? "L'événement doit avoir lieu dans le futur."
            : null;

        DurationError = int.TryParse(DurationHours, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h is >= 1 and <= 72
            ? null
            : "Durée entre 1 et 72 heures.";

        CapacityError = string.IsNullOrWhiteSpace(Capacity)
                        || (int.TryParse(Capacity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) && c is >= 2 and <= 10000)
            ? null
            : "Capacité entre 2 et 10 000 (laisser vide pour illimité).";

        return new[] { TitleError, DescriptionError, CategoryError, CityError, AddressError, DateError, DurationError, CapacityError }.All(e => e is null);
    }

    private bool CanPublish() => !IsSubmitting;

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync()
    {
        FormError = null;
        if (!Validate())
        {
            FormError = "Corrigez les champs signalés.";
            return;
        }

        IsSubmitting = true;
        try
        {
            var start = ComputeStart()!.Value;
            var created = await events.CreateAsync(new CreateEventRequest(
                session.CurrentUserId,
                Title,
                Description,
                Category!,
                City,
                Country,
                Address,
                start,
                start.AddHours(int.Parse(DurationHours, CultureInfo.InvariantCulture)),
                string.IsNullOrWhiteSpace(Capacity) ? null : int.Parse(Capacity, CultureInfo.InvariantCulture),
                SelectedCommunity?.Id,
                ImagePath));

            toast.Show("Événement publié.", ToastKind.Success);
            Navigation.ReplaceWith<EventDetailsViewModel>(created.Id);
        }
        catch (Exception ex)
        {
            FormError = ErrorMessages.From(ex);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private void OnImageSelected(ImageOption option)
    {
        foreach (var image in Images)
        {
            image.IsSelected = ReferenceEquals(image, option);
        }

        ImagePath = option.Path;
    }
}
