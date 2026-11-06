using BeninConnect.App.Core.DependencyInjection;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Authentication.Contracts;
using BeninConnect.App.Features.Authentication.Models;
using BeninConnect.App.MockData;
using BeninConnect.App.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Tests;

/// <summary>Conteneur complet de l'app, sans latence ni splash, préférences en mémoire, dialogues auto-confirmés.</summary>
public sealed class TestHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public TestHost(bool confirmDialogs = true)
    {
        var services = new ServiceCollection();
        services.AddBeninConnect(new AppOptions
        {
            SimulatedLatency = TimeSpan.Zero,
            SplashDuration = TimeSpan.Zero,
            PreferencesFilePath = null,
            ApplyThemeToApplication = false
        });
        services.AddSingleton<IDialogService>(new FakeDialogService(confirmDialogs));
        _provider = services.BuildServiceProvider();
    }

    public T Get<T>() where T : notnull => _provider.GetRequiredService<T>();

    public MockDataStore Store => Get<MockDataStore>();
    public INavigationService Navigation => Get<INavigationService>();
    public ToastService Toasts => Get<ToastService>();

    public User SignInDemo()
    {
        var result = Get<IAuthService>().LoginAsync(DemoAccount.Email, DemoAccount.Password).GetAwaiter().GetResult();
        var user = result.User ?? throw new InvalidOperationException("Connexion de démonstration impossible.");
        Get<ISessionService>().SignIn(user);
        return user;
    }

    /// <summary>Ouvre un écran via la navigation et attend son chargement.</summary>
    public async Task<T> OpenAsync<T>(object? parameter = null) where T : App.Core.Mvvm.ViewModelBase
    {
        Navigation.NavigateTo<T>(parameter);
        await Navigation.LastNavigationTask;
        return (T)Navigation.CurrentViewModel!;
    }

    public void Dispose() => _provider.Dispose();
}

public sealed class FakeDialogService(bool answer) : IDialogService
{
    public int ConfirmCount { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Confirmer", string cancelText = "Annuler", bool isDestructive = false)
    {
        ConfirmCount++;
        return Task.FromResult(answer);
    }

    public Task AlertAsync(string title, string message, string closeText = "Fermer") => Task.CompletedTask;
}
