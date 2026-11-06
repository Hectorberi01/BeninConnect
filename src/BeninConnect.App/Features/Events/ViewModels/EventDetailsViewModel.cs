using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Profile.ViewModels;
using BeninConnect.App.Shared.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Events.ViewModels;

public sealed partial class EventDetailsViewModel(
    INavigationService navigation,
    IEventRepository events,
    IUserRepository users,
    ICommunityRepository communities,
    EventItemFactory factory) : PageViewModel(navigation)
{
    private Guid _eventId;
    private Guid _organizerId;
    private Guid? _communityId;

    [ObservableProperty] private EventItemViewModel? _event;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _scheduleText = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _organizerName = string.Empty;
    [ObservableProperty] private string _organizerInitials = string.Empty;
    [ObservableProperty] private string _organizerColor = "#087F5B";
    [ObservableProperty] private string? _communityName;
    [ObservableProperty] private string _capacityText = string.Empty;

    public bool HasCommunity => CommunityName is not null;

    partial void OnCommunityNameChanged(string? value) => OnPropertyChanged(nameof(HasCommunity));

    public override Task OnNavigatedToAsync(object? parameter)
    {
        _eventId = parameter is Guid id ? id : Guid.Empty;
        return ReloadAsync();
    }

    protected override Task ReloadAsync() => LoadAsync(async ct =>
    {
        var item = await events.GetByIdAsync(_eventId, ct) ?? throw new NotFoundException("Événement");
        Event = factory.Create(item);
        Description = item.Description;
        ScheduleText = TimeFormatter.EventSchedule(item.StartsAt, item.EndsAt);
        Address = item.Address;
        Location = string.IsNullOrEmpty(item.Country) ? item.City : $"{item.City}, {item.Country}";
        CapacityText = item.Capacity is { } c ? $"Capacité : {c} places" : "Entrée libre, sans limite de places";

        _organizerId = item.OrganizerId;
        if (await users.GetByIdAsync(item.OrganizerId, ct) is { } organizer)
        {
            OrganizerName = organizer.DisplayName;
            OrganizerInitials = organizer.Initials;
            OrganizerColor = organizer.AvatarColor;
        }

        _communityId = item.CommunityId;
        CommunityName = item.CommunityId is { } cid ? (await communities.GetByIdAsync(cid, ct))?.Name : null;
        return true;
    });

    [RelayCommand]
    private void OpenOrganizer() => Navigation.NavigateTo<ProfileViewModel>(_organizerId);

    [RelayCommand]
    private void OpenCommunity()
    {
        if (_communityId is { } id)
        {
            Navigation.NavigateTo<CommunityDetailsViewModel>(id);
        }
    }
}
