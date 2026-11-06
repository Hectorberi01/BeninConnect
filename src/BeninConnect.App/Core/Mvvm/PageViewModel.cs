using BeninConnect.App.Core.Navigation;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Core.Mvvm;

/// <summary>Écran secondaire poussé sur la pile, avec bouton retour.</summary>
public abstract partial class PageViewModel(INavigationService navigation) : ViewModelBase
{
    protected INavigationService Navigation { get; } = navigation;

    [RelayCommand]
    private void GoBack() => Navigation.GoBack();
}
