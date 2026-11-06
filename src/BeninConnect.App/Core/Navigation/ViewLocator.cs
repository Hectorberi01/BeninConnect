using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Features.Authentication.ViewModels;
using BeninConnect.App.Features.Authentication.Views;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Communities.Views;
using BeninConnect.App.Features.Events.ViewModels;
using BeninConnect.App.Features.Events.Views;
using BeninConnect.App.Features.Feed.ViewModels;
using BeninConnect.App.Features.Feed.Views;
using BeninConnect.App.Features.Home.ViewModels;
using BeninConnect.App.Features.Home.Views;
using BeninConnect.App.Features.Messaging.ViewModels;
using BeninConnect.App.Features.Messaging.Views;
using BeninConnect.App.Features.Notifications.ViewModels;
using BeninConnect.App.Features.Notifications.Views;
using BeninConnect.App.Features.Onboarding.ViewModels;
using BeninConnect.App.Features.Onboarding.Views;
using BeninConnect.App.Features.Profile.ViewModels;
using BeninConnect.App.Features.Profile.Views;
using BeninConnect.App.Features.Search.ViewModels;
using BeninConnect.App.Features.Search.Views;
using BeninConnect.App.Features.Settings.ViewModels;
using BeninConnect.App.Features.Settings.Views;
using BeninConnect.App.Shell;

namespace BeninConnect.App.Core.Navigation;

/// <summary>
/// Associe chaque ViewModel à sa vue. Table explicite (pas de réflexion) : compatible trimming Android/iOS.
/// La vue est mise en cache par instance de ViewModel pour conserver le défilement au retour.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Func<Control>> Factories = new()
    {
        [typeof(SplashViewModel)] = () => new SplashView(),
        [typeof(OnboardingViewModel)] = () => new OnboardingView(),
        [typeof(LoginViewModel)] = () => new LoginView(),
        [typeof(RegisterViewModel)] = () => new RegisterView(),
        [typeof(ForgotPasswordViewModel)] = () => new ForgotPasswordView(),
        [typeof(VerifyEmailViewModel)] = () => new VerifyEmailView(),
        [typeof(ShellViewModel)] = () => new ShellView(),
        [typeof(HomeViewModel)] = () => new HomeView(),
        [typeof(CreatePostViewModel)] = () => new CreatePostView(),
        [typeof(PostDetailsViewModel)] = () => new PostDetailsView(),
        [typeof(CommunitiesViewModel)] = () => new CommunitiesView(),
        [typeof(CommunityDetailsViewModel)] = () => new CommunityDetailsView(),
        [typeof(CreateCommunityViewModel)] = () => new CreateCommunityView(),
        [typeof(CommunityMembersViewModel)] = () => new CommunityMembersView(),
        [typeof(ConversationsViewModel)] = () => new ConversationsView(),
        [typeof(ConversationViewModel)] = () => new ConversationView(),
        [typeof(NewConversationViewModel)] = () => new NewConversationView(),
        [typeof(EventsViewModel)] = () => new EventsView(),
        [typeof(EventDetailsViewModel)] = () => new EventDetailsView(),
        [typeof(CreateEventViewModel)] = () => new CreateEventView(),
        [typeof(SearchViewModel)] = () => new SearchView(),
        [typeof(NotificationsViewModel)] = () => new NotificationsView(),
        [typeof(ProfileViewModel)] = () => new ProfileView(),
        [typeof(EditProfileViewModel)] = () => new EditProfileView(),
        [typeof(SettingsViewModel)] = () => new SettingsView(),
    };

    private readonly ConditionalWeakTable<object, Control> _cache = new();

    public static bool IsRegistered(Type viewModelType) => Factories.ContainsKey(viewModelType);

    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        if (_cache.TryGetValue(param, out var cached))
        {
            return cached;
        }

        if (!Factories.TryGetValue(param.GetType(), out var factory))
        {
            return new TextBlock { Text = $"Vue introuvable : {param.GetType().Name}" };
        }

        var view = factory();
        _cache.AddOrUpdate(param, view);
        return view;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
