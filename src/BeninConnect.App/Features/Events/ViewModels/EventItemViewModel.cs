using System.Globalization;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Events.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Features.Events.ViewModels;

public sealed class EventItemFactory(
    IEventRepository events,
    INavigationService navigation,
    IToastService toast,
    ISessionService session,
    IMessenger messenger,
    TimeProvider clock)
{
    internal IEventRepository Events => events;
    internal INavigationService Navigation => navigation;
    internal IToastService Toast => toast;
    internal ISessionService Session => session;
    internal IMessenger Messenger => messenger;
    internal TimeProvider Clock => clock;

    public EventItemViewModel Create(CommunityEvent item) => new(item, this);

    public IReadOnlyList<EventItemViewModel> Create(IEnumerable<CommunityEvent> items) => items.Select(Create).ToList();
}

/// <summary>Carte d'événement avec inscription / désinscription.</summary>
public sealed partial class EventItemViewModel : ObservableObject
{
    private readonly EventItemFactory _ctx;

    internal EventItemViewModel(CommunityEvent item, EventItemFactory context)
    {
        _ctx = context;
        _event = item;

        context.Messenger.Register<EventItemViewModel, EventChangedMessage>(this, static (r, m) =>
        {
            if (m.Event.Id == r.Id)
            {
                r.Event = m.Event;
            }
        });
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParticipantsText), nameof(IsParticipating), nameof(IsFull), nameof(ParticipationLabel), nameof(ParticipationActionText), nameof(CanParticipate))]
    private CommunityEvent _event;

    [ObservableProperty]
    private bool _isBusy;

    public Guid Id => Event.Id;
    public string Title => Event.Title;
    public string Category => Event.Category;
    public string City => Event.City;
    public string? ImagePath => Event.ImagePath;
    public string Day => Event.StartsAt.Day.ToString(CultureInfo.InvariantCulture);
    public string Month => TimeFormatter.MonthShort(Event.StartsAt);
    public string DateText => $"{TimeFormatter.LongDate(Event.StartsAt)} · {TimeFormatter.Clock(Event.StartsAt)}";
    public string ShortDateText => $"{TimeFormatter.ShortDate(Event.StartsAt, _ctx.Clock.GetLocalNow())} · {TimeFormatter.Clock(Event.StartsAt)}";

    public string ParticipantsText => Event.Capacity is { } capacity
        ? $"{Event.ParticipantCount} / {capacity} participants"
        : $"{Event.ParticipantCount} participant{(Event.ParticipantCount > 1 ? "s" : "")}";

    public bool IsParticipating => _ctx.Session.CurrentUser is { } me && Event.IsParticipating(me.Id);
    public bool IsFull => Event.IsFull && !IsParticipating;
    public bool CanParticipate => !IsFull;

    public string ParticipationLabel => IsParticipating ? "Inscrit" : IsFull ? "Complet" : "Participer";

    public string ParticipationActionText => IsParticipating ? "Inscrit · Annuler ma participation" : IsFull ? "Événement complet" : "Participer à l'événement";

    [RelayCommand]
    private void Open() => _ctx.Navigation.NavigateTo<EventDetailsViewModel>(Id);

    [RelayCommand]
    private async Task ToggleParticipationAsync()
    {
        if (IsBusy || _ctx.Session.CurrentUser is not { } me)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var participating = await _ctx.Events.ToggleParticipationAsync(Id, me.Id);
            _ctx.Toast.Show(
                participating ? $"Inscription confirmée : {Title}" : "Votre inscription a été annulée.",
                participating ? ToastKind.Success : ToastKind.Info);
        }
        catch (Exception ex)
        {
            _ctx.Toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
