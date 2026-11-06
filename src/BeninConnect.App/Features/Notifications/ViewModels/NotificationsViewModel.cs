using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Events.ViewModels;
using BeninConnect.App.Features.Feed.ViewModels;
using BeninConnect.App.Features.Messaging.ViewModels;
using BeninConnect.App.Features.Notifications.Contracts;
using BeninConnect.App.Features.Notifications.Models;
using BeninConnect.App.Features.Profile.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Notifications.ViewModels;

public sealed partial class NotificationItemViewModel(Notification notification, DateTimeOffset now, Func<NotificationItemViewModel, Task> open, Func<NotificationItemViewModel, Task> markRead)
    : ObservableObject
{
    public Notification Notification { get; } = notification;
    public Guid Id => Notification.Id;
    public string Title => Notification.Title;
    public string Body => Notification.Body;
    public string TimeText => TimeFormatter.Relative(Notification.CreatedAt, now);

    /// <summary>Libellé de la catégorie affiché sous le titre.</summary>
    public string CategoryLabel => Notification.Category switch
    {
        NotificationCategory.Social => "Social",
        NotificationCategory.Communities => "Communautés",
        NotificationCategory.Messages => "Messages",
        NotificationCategory.Events => "Événements",
        _ => "Système"
    };

    public bool IsSocial => Notification.Category == NotificationCategory.Social;
    public bool IsCommunity => Notification.Category == NotificationCategory.Communities;
    public bool IsMessage => Notification.Category == NotificationCategory.Messages;
    public bool IsEvent => Notification.Category == NotificationCategory.Events;
    public bool IsSystem => Notification.Category == NotificationCategory.System;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnread))]
    private bool _isRead = notification.IsRead;

    public bool IsUnread => !IsRead;

    [RelayCommand]
    private Task Open() => open(this);

    [RelayCommand]
    private Task MarkRead() => markRead(this);
}

/// <summary>Notifications par catégorie : marquer comme lu, tout marquer, ouvrir le contenu concerné.</summary>
public sealed partial class NotificationsViewModel(
    INavigationService navigation,
    INotificationRepository notifications,
    ISessionService session,
    IToastService toast,
    TimeProvider clock) : PageViewModel(navigation)
{
    private const string All = "*";
    private string _category = All;

    public ObservableCollection<Chip> Categories { get; } = [];
    public ObservableCollection<NotificationItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    [NotifyCanExecuteChangedFor(nameof(MarkAllAsReadCommand))]
    private int _unreadCount;

    public bool HasUnread => UnreadCount > 0;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        Categories.Add(new Chip(All, "Toutes", OnCategorySelected, isSelected: true));
        foreach (var (key, label) in new[]
                 {
                     (nameof(NotificationCategory.Social), "Social"),
                     (nameof(NotificationCategory.Communities), "Communautés"),
                     (nameof(NotificationCategory.Messages), "Messages"),
                     (nameof(NotificationCategory.Events), "Événements"),
                     (nameof(NotificationCategory.System), "Système"),
                 })
        {
            Categories.Add(new Chip(key, label, OnCategorySelected));
        }

        return ReloadAsync();
    }

    protected override Task ReloadAsync() => LoadAsync(async ct =>
    {
        var me = session.CurrentUserId;
        NotificationCategory? category = _category == All ? null : Enum.Parse<NotificationCategory>(_category);
        var list = await notifications.GetForUserAsync(me, category, ct);
        var now = clock.GetLocalNow();

        Items.Clear();
        foreach (var n in list)
        {
            Items.Add(new NotificationItemViewModel(n, now, OpenAsync, MarkReadAsync));
        }

        UnreadCount = await notifications.GetUnreadCountAsync(me, ct);
        return Items.Count > 0;
    });

    private void OnCategorySelected(Chip chip)
    {
        Chip.SelectExclusive(Categories, chip);
        _category = chip.Key;
        _ = ReloadAsync();
    }

    private bool CanMarkAll() => HasUnread;

    [RelayCommand(CanExecute = nameof(CanMarkAll))]
    private async Task MarkAllAsReadAsync()
    {
        try
        {
            await notifications.MarkAllAsReadAsync(session.CurrentUserId);
            foreach (var item in Items)
            {
                item.IsRead = true;
            }

            UnreadCount = 0;
            toast.Show("Toutes les notifications sont lues.");
        }
        catch (Exception ex)
        {
            toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
    }

    private async Task MarkReadAsync(NotificationItemViewModel item)
    {
        if (item.IsRead)
        {
            return;
        }

        try
        {
            await notifications.MarkAsReadAsync(item.Id);
            item.IsRead = true;
            UnreadCount = Math.Max(0, UnreadCount - 1);
        }
        catch (Exception ex)
        {
            toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
    }

    private async Task OpenAsync(NotificationItemViewModel item)
    {
        await MarkReadAsync(item);

        var n = item.Notification;
        switch (n.Target)
        {
            case NotificationTarget.Post when n.TargetId is { } id:
                Navigation.NavigateTo<PostDetailsViewModel>(id);
                break;
            case NotificationTarget.Community when n.TargetId is { } id:
                Navigation.NavigateTo<CommunityDetailsViewModel>(id);
                break;
            case NotificationTarget.Conversation when n.TargetId is { } id:
                Navigation.NavigateTo<ConversationViewModel>(id);
                break;
            case NotificationTarget.Event when n.TargetId is { } id:
                Navigation.NavigateTo<EventDetailsViewModel>(id);
                break;
            case NotificationTarget.Profile when n.TargetId is { } id:
                Navigation.NavigateTo<ProfileViewModel>(id);
                break;
            default:
                toast.Show(n.Body);
                break;
        }
    }
}
