using BeninConnect.App.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Core.Mvvm;

public enum ViewState
{
    Idle,
    Loading,
    Content,
    Empty,
    Error
}

/// <summary>
/// Base de tous les écrans : cycle de navigation + état de chargement (chargement, contenu, vide, erreur).
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(IsEmpty), nameof(IsError), nameof(IsContent))]
    private ViewState _state = ViewState.Idle;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsLoading => State == ViewState.Loading;
    public bool IsEmpty => State == ViewState.Empty;
    public bool IsError => State == ViewState.Error;
    public bool IsContent => State == ViewState.Content;

    /// <summary>Appelé quand l'écran est poussé sur la pile (ou activé pour un onglet).</summary>
    public virtual Task OnNavigatedToAsync(object? parameter) => Task.CompletedTask;

    /// <summary>Appelé quand l'écran redevient visible après un retour arrière.</summary>
    public virtual Task OnResumedAsync() => Task.CompletedTask;

    /// <summary>Appelé quand l'écran est retiré de la pile.</summary>
    public virtual void OnNavigatedFrom() => _loadCts?.Cancel();

    /// <summary>Retourne true si l'écran gère lui-même le retour (ex : onboarding, onglets).</summary>
    public virtual bool OnBackRequested() => false;

    [RelayCommand]
    private Task RetryAsync() => ReloadAsync();

    protected virtual Task ReloadAsync() => Task.CompletedTask;

    /// <summary>Annule le chargement en cours (ex : recherche vidée pendant une requête).</summary>
    protected void CancelPendingLoad() => _loadCts?.Cancel();

    /// <summary>Exécute un chargement en gérant les états. Le loader retourne true s'il y a du contenu.</summary>
    protected async Task LoadAsync(Func<CancellationToken, Task<bool>> loader, bool showLoading = true)
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        if (showLoading)
        {
            State = ViewState.Loading;
        }

        ErrorMessage = null;

        try
        {
            var hasContent = await loader(cts.Token);
            if (!cts.IsCancellationRequested)
            {
                State = hasContent ? ViewState.Content : ViewState.Empty;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Chargement remplacé par un plus récent.
        }
        catch (Exception ex)
        {
            ErrorMessage = ErrorMessages.From(ex);
            State = ViewState.Error;
        }
    }
}
