using BeninConnect.App.Core.Mvvm;

namespace BeninConnect.App.Core.Navigation;

public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }

    bool CanGoBack { get; }

    /// <summary>Pile courante (lecture seule), utile pour les tests.</summary>
    IReadOnlyList<ViewModelBase> Stack { get; }

    /// <summary>Tâche d'initialisation du dernier écran ouvert (attendue par les tests).</summary>
    Task LastNavigationTask { get; }

    event EventHandler? Navigated;

    /// <summary>Ouvre un écran. Le paramètre (id de publication, de communauté...) est transmis à OnNavigatedToAsync.</summary>
    void NavigateTo<TViewModel>(object? parameter = null)
        where TViewModel : ViewModelBase;

    /// <summary>Remplace l'écran courant (ex : nouvelle conversation → conversation).</summary>
    void ReplaceWith<TViewModel>(object? parameter = null)
        where TViewModel : ViewModelBase;

    /// <summary>Vide la pile et ouvre un écran racine (connexion, accueil...).</summary>
    void ResetTo<TViewModel>(object? parameter = null)
        where TViewModel : ViewModelBase;

    void GoBack();

    /// <summary>Retour système (bouton Android) : l'écran peut l'intercepter, sinon retour dans l'historique.</summary>
    bool TryHandleBack();
}
