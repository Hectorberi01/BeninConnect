using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Authentication.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Onboarding.ViewModels;

public sealed record OnboardingPage(string Title, string Description, string ImagePath);

public sealed partial class OnboardingDot(bool isActive) : ObservableObject
{
    [ObservableProperty]
    private bool _isActive = isActive;
}

public sealed partial class OnboardingViewModel(INavigationService navigation, IPreferencesService preferences) : ViewModelBase
{
    public IReadOnlyList<OnboardingPage> Pages { get; } =
    [
        new("Connectez-vous à votre communauté",
            "Retrouvez les Béninoises et Béninois de votre ville, de votre pays d'accueil et du monde entier.",
            "Onboarding/onboarding_1.png"),
        new("Partagez vos expériences et découvrez les événements",
            "Publiez, commentez, échangez des conseils et participez aux rencontres près de chez vous.",
            "Onboarding/onboarding_2.png"),
        new("Développez votre réseau partout dans le monde",
            "Études, emploi, entrepreneuriat, retour au pays : avancez avec le soutien de la diaspora.",
            "Onboarding/onboarding_3.png"),
    ];

    public IReadOnlyList<OnboardingDot> Dots { get; } = [new(true), new(false), new(false)];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Current), nameof(IsLast), nameof(IsNotLast))]
    private int _index;

    public OnboardingPage Current => Pages[Index];
    public bool IsLast => Index == Pages.Count - 1;
    public bool IsNotLast => !IsLast;

    partial void OnIndexChanged(int value)
    {
        for (var i = 0; i < Dots.Count; i++)
        {
            Dots[i].IsActive = i == value;
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (IsLast)
        {
            Finish();
            return;
        }

        Index++;
    }

    [RelayCommand]
    private void Skip() => Finish();

    [RelayCommand]
    private void Start() => Finish();

    public override bool OnBackRequested()
    {
        if (Index == 0)
        {
            return false;
        }

        Index--;
        return true;
    }

    private void Finish()
    {
        preferences.SetBool(PreferenceKeys.OnboardingCompleted, true);
        navigation.ResetTo<LoginViewModel>();
    }
}
