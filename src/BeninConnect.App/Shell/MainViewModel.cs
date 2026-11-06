using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Onboarding.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BeninConnect.App.Shell;

/// <summary>Racine de l'application : écran courant + surimpressions (dialogue, toast).</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public MainViewModel(INavigationService navigation, DialogService dialogs, ToastService toasts)
    {
        _navigation = navigation;
        Dialogs = dialogs;
        Toasts = toasts;
        _navigation.Navigated += (_, _) => OnPropertyChanged(nameof(CurrentPage));
    }

    public ViewModelBase? CurrentPage => _navigation.CurrentViewModel;

    public DialogService Dialogs { get; }

    public ToastService Toasts { get; }

    public void Start() => _navigation.ResetTo<SplashViewModel>();

    /// <summary>Bouton retour système (Android). Retourne false pour laisser le système fermer l'app.</summary>
    public bool HandleBack()
    {
        if (Dialogs.Current is { } dialog)
        {
            dialog.Cancel();
            return true;
        }

        return _navigation.TryHandleBack();
    }
}
