using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Authentication.ViewModels;
using BeninConnect.App.Features.Events.ViewModels;
using BeninConnect.App.Features.Notifications.ViewModels;
using BeninConnect.App.Features.Onboarding.ViewModels;
using BeninConnect.App.Features.Settings.ViewModels;
using BeninConnect.App.Shell;

namespace BeninConnect.Tests;

public class NavigationTests
{
    [Fact]
    public async Task Navigate_and_go_back_preserve_the_previous_screen()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var shell = await host.OpenAsync<ShellViewModel>();

        await host.OpenAsync<NotificationsViewModel>();
        Assert.True(host.Navigation.CanGoBack);

        host.Navigation.GoBack();
        Assert.Same(shell, host.Navigation.CurrentViewModel);
        Assert.False(host.Navigation.CanGoBack);
    }

    [Fact]
    public async Task Double_tap_on_same_destination_opens_it_once()
    {
        using var host = new TestHost();
        host.SignInDemo();
        await host.OpenAsync<ShellViewModel>();

        host.Navigation.NavigateTo<EventsViewModel>();
        host.Navigation.NavigateTo<EventsViewModel>();

        Assert.Equal(2, host.Navigation.Stack.Count);
    }

    [Fact]
    public async Task Back_on_secondary_tab_returns_to_home_tab()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var shell = await host.OpenAsync<ShellViewModel>();
        shell.ShowProfileCommand.Execute(null);

        Assert.True(host.Navigation.TryHandleBack());
        Assert.True(shell.IsHomeSelected);
        Assert.False(host.Navigation.TryHandleBack()); // racine : l'OS ferme l'app
    }

    [Fact]
    public async Task Switching_tabs_keeps_view_model_state()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var shell = await host.OpenAsync<ShellViewModel>();
        await shell.ActivationTask;
        var home = shell.Home;
        await home.LoadMoreCommand.ExecuteAsync(null);
        var count = home.Posts.Count;

        shell.ShowCommunitiesCommand.Execute(null);
        shell.ShowHomeCommand.Execute(null);

        Assert.Same(home, shell.Home);
        Assert.Equal(count, home.Posts.Count);
    }

    [Fact]
    public async Task First_launch_goes_to_onboarding_then_login()
    {
        using var host = new TestHost();
        host.Navigation.NavigateTo<SplashViewModel>();
        await host.Navigation.LastNavigationTask;
        var onboarding = Assert.IsType<OnboardingViewModel>(host.Navigation.CurrentViewModel);

        onboarding.NextCommand.Execute(null);
        onboarding.NextCommand.Execute(null);
        Assert.True(onboarding.IsLast);
        onboarding.StartCommand.Execute(null);

        Assert.IsType<LoginViewModel>(host.Navigation.CurrentViewModel);
        Assert.True(host.Get<IPreferencesService>().GetBool(PreferenceKeys.OnboardingCompleted));
    }

    [Fact]
    public async Task Remembered_account_skips_login()
    {
        using var host = new TestHost();
        var prefs = host.Get<IPreferencesService>();
        prefs.SetBool(PreferenceKeys.OnboardingCompleted, true);
        prefs.Set(PreferenceKeys.RememberedAccount, "demo@beninconnect.test");

        host.Navigation.NavigateTo<SplashViewModel>();
        await host.Navigation.LastNavigationTask;

        Assert.IsType<ShellViewModel>(host.Navigation.CurrentViewModel);
        Assert.True(host.Get<ISessionService>().IsAuthenticated);
    }

    [Fact]
    public async Task Demo_login_opens_shell()
    {
        using var host = new TestHost();
        var login = await host.OpenAsync<LoginViewModel>();
        login.UseDemoAccountCommand.Execute(null);

        await login.LoginCommand.ExecuteAsync(null);

        Assert.IsType<ShellViewModel>(host.Navigation.CurrentViewModel);
        Assert.Single(host.Navigation.Stack);
    }

    [Fact]
    public async Task Logout_returns_to_login_and_clears_session()
    {
        using var host = new TestHost();
        host.SignInDemo();
        await host.OpenAsync<ShellViewModel>();
        var settings = await host.OpenAsync<SettingsViewModel>();

        await settings.LogoutCommand.ExecuteAsync(null);

        Assert.IsType<LoginViewModel>(host.Navigation.CurrentViewModel);
        Assert.False(host.Get<ISessionService>().IsAuthenticated);
        Assert.Null(host.Get<IPreferencesService>().Get(PreferenceKeys.RememberedAccount));
    }

    [Fact]
    public void Every_screen_has_a_registered_view()
    {
        var screens = typeof(BeninConnect.App.App).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(BeninConnect.App.Core.Mvvm.ViewModelBase)));

        Assert.All(screens, t => Assert.True(BeninConnect.App.Core.Navigation.ViewLocator.IsRegistered(t), $"{t.Name} sans vue"));
    }
}
