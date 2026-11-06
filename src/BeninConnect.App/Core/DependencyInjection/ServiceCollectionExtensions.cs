using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Authentication.Contracts;
using BeninConnect.App.Features.Authentication.ViewModels;
using BeninConnect.App.Features.Communities.Contracts;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Events.ViewModels;
using BeninConnect.App.Features.Feed.Contracts;
using BeninConnect.App.Features.Feed.ViewModels;
using BeninConnect.App.Features.Home.ViewModels;
using BeninConnect.App.Features.Messaging.Contracts;
using BeninConnect.App.Features.Messaging.ViewModels;
using BeninConnect.App.Features.Notifications.Contracts;
using BeninConnect.App.Features.Notifications.ViewModels;
using BeninConnect.App.Features.Onboarding.ViewModels;
using BeninConnect.App.Features.Profile.ViewModels;
using BeninConnect.App.Features.Search.Services;
using BeninConnect.App.Features.Search.ViewModels;
using BeninConnect.App.Features.Settings.ViewModels;
using BeninConnect.App.Infrastructure.Api;
using BeninConnect.App.MockData;
using BeninConnect.App.MockData.Factories;
using BeninConnect.App.MockData.Repositories;
using BeninConnect.App.Shared.Contracts;
using BeninConnect.App.Shell;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.App.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBeninConnect(this IServiceCollection services, AppOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddCoreServices(options);
        services.AddDataSources(options);
        services.AddViewModels();
        return services;
    }

    private static IServiceCollection AddCoreServices(this IServiceCollection services, AppOptions options)
    {
        // Messenger dédié (pas le singleton statique) : chaque conteneur, donc chaque test, est isolé.
        services.AddSingleton<IMessenger>(_ => new WeakReferenceMessenger());
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISessionService, SessionService>();

        services.AddSingleton<ToastService>();
        services.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());

        services.AddSingleton<IPreferencesService>(_ => options.PreferencesFilePath is { } path
            ? new JsonFilePreferencesService(path)
            : new InMemoryPreferencesService());
        services.AddSingleton<IThemeService, ThemeService>();

        services.AddSingleton<DeveloperOptions>();
        services.AddSingleton<IDeveloperOptions>(sp => sp.GetRequiredService<DeveloperOptions>());

        services.AddSingleton<PostItemFactory>();
        services.AddSingleton<CommunityItemFactory>();
        services.AddSingleton<EventItemFactory>();
        services.AddSingleton<ISearchService, SearchService>();
        return services;
    }

    /// <summary>
    /// Sélection de la source de données. Les ViewModels ne dépendent que des interfaces :
    /// la source est choisie au démarrage (Paramètres › Mode développeur, ou BENINCONNECT_API_URL).
    /// </summary>
    private static IServiceCollection AddDataSources(this IServiceCollection services, AppOptions options)
    {
        if (!options.UseMockData)
        {
            // API ASP.NET Core (dossier server/). Un HttpClient unique, réutilisé par toutes les requêtes.
            services.AddSingleton(sp => new ApiClient(
                new HttpClient { BaseAddress = WithTrailingSlash(options.ApiBaseUrl ?? new Uri(AppOptions.DefaultApiUrl)), Timeout = options.ApiTimeout },
                sp.GetRequiredService<IPreferencesService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<IAuthService, ApiAuthService>();
            services.AddSingleton<IUserRepository, ApiUserRepository>();
            services.AddSingleton<IPostRepository, ApiPostRepository>();
            services.AddSingleton<ICommentRepository, ApiCommentRepository>();
            services.AddSingleton<ICommunityRepository, ApiCommunityRepository>();
            services.AddSingleton<IConversationRepository, ApiConversationRepository>();
            services.AddSingleton<IEventRepository, ApiEventRepository>();
            services.AddSingleton<INotificationRepository, ApiNotificationRepository>();
            return services;
        }

        // Un seul store partagé : toutes les modifications restent cohérentes pendant la session.
        services.AddSingleton(sp => MockDataFactory.Create(sp.GetRequiredService<TimeProvider>().GetLocalNow()));

        services.AddSingleton<IAuthService, MockAuthService>();
        services.AddSingleton<IUserRepository, MockUserRepository>();
        services.AddSingleton<IPostRepository, MockPostRepository>();
        services.AddSingleton<ICommentRepository, MockCommentRepository>();
        services.AddSingleton<ICommunityRepository, MockCommunityRepository>();
        services.AddSingleton<IConversationRepository, MockConversationRepository>();
        services.AddSingleton<IEventRepository, MockEventRepository>();
        services.AddSingleton<INotificationRepository, MockNotificationRepository>();
        return services;
    }

    private static Uri WithTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");

    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();

        // Écrans : une nouvelle instance à chaque navigation.
        services.AddTransient<SplashViewModel>();
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<ForgotPasswordViewModel>();
        services.AddTransient<VerifyEmailViewModel>();

        services.AddTransient<ShellViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<CreatePostViewModel>();
        services.AddTransient<PostDetailsViewModel>();

        services.AddTransient<CommunitiesViewModel>();
        services.AddTransient<CommunityDetailsViewModel>();
        services.AddTransient<CreateCommunityViewModel>();
        services.AddTransient<CommunityMembersViewModel>();

        services.AddTransient<ConversationsViewModel>();
        services.AddTransient<ConversationViewModel>();
        services.AddTransient<NewConversationViewModel>();

        services.AddTransient<EventsViewModel>();
        services.AddTransient<EventDetailsViewModel>();
        services.AddTransient<CreateEventViewModel>();

        services.AddTransient<SearchViewModel>();
        services.AddTransient<NotificationsViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<EditProfileViewModel>();
        services.AddTransient<SettingsViewModel>();
        return services;
    }
}
