using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Authentication.Contracts;
using BeninConnect.App.Features.Authentication.ViewModels;
using BeninConnect.App.Shell;

namespace BeninConnect.App.Features.Onboarding.ViewModels;

/// <summary>Écran de lancement : décide entre onboarding, connexion ou accueil (session mémorisée).</summary>
public sealed class SplashViewModel(
    INavigationService navigation,
    IPreferencesService preferences,
    IAuthService auth,
    ISessionService session,
    AppOptions options) : ViewModelBase
{
    public string Version => "Version 1.0 · démonstration";

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        if (options.SplashDuration > TimeSpan.Zero)
        {
            await Task.Delay(options.SplashDuration);
        }

        if (!preferences.GetBool(PreferenceKeys.OnboardingCompleted))
        {
            navigation.ResetTo<OnboardingViewModel>();
            return;
        }

        if (preferences.Get(PreferenceKeys.RememberedAccount) is { } email)
        {
            try
            {
                if (await auth.RestoreSessionAsync(email) is { } user)
                {
                    session.SignIn(user);
                    navigation.ResetTo<ShellViewModel>();
                    return;
                }
            }
            catch (Exception)
            {
                // En cas d'échec (erreurs simulées), retour à la connexion.
            }
        }

        navigation.ResetTo<LoginViewModel>();
    }
}
