using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Profile.ViewModels;

/// <summary>Ligne « membre » réutilisable (liste des membres, recherche, nouvelle conversation).</summary>
public sealed partial class UserItemViewModel(User user, string subtitle, Action<User> onSelected, string? badge = null) : ObservableObject
{
    public User User { get; } = user;
    public Guid Id => User.Id;
    public string Name => User.DisplayName;
    public string Initials => User.Initials;
    public string AvatarColor => User.AvatarColor;
    public string Subtitle { get; } = subtitle;
    public string? Badge { get; } = badge;
    public bool HasBadge => !string.IsNullOrEmpty(Badge);

    [RelayCommand]
    private void Open() => onSelected(User);
}
