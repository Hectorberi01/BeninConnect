using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Core.Services;

public interface ISessionService
{
    User? CurrentUser { get; }

    bool IsAuthenticated { get; }

    /// <summary>Identifiant de l'utilisateur connecté (exception si aucune session).</summary>
    Guid CurrentUserId { get; }

    void SignIn(User user);

    void Update(User user);

    void SignOut();
}

public sealed record SessionChangedMessage(User? User);

/// <summary>Session simulée en mémoire.</summary>
public sealed class SessionService(IMessenger messenger) : ObservableObject, ISessionService
{
    private User? _currentUser;

    public User? CurrentUser
    {
        get => _currentUser;
        private set
        {
            if (SetProperty(ref _currentUser, value))
            {
                OnPropertyChanged(nameof(IsAuthenticated));
                messenger.Send(new SessionChangedMessage(value));
            }
        }
    }

    public bool IsAuthenticated => CurrentUser is not null;

    public Guid CurrentUserId =>
        CurrentUser?.Id ?? throw new InvalidOperationException("Aucun utilisateur connecté.");

    public void SignIn(User user) => CurrentUser = user;

    public void Update(User user)
    {
        if (CurrentUser?.Id == user.Id)
        {
            CurrentUser = user;
        }
    }

    public void SignOut() => CurrentUser = null;
}
