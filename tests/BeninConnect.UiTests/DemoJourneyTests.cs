using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Authentication.Views;
using BeninConnect.App.Features.Communities.Components;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Features.Events.Components;
using BeninConnect.App.Features.Events.Views;
using BeninConnect.App.Features.Feed.Components;
using BeninConnect.App.Features.Feed.ViewModels;
using BeninConnect.App.Features.Feed.Views;
using BeninConnect.App.Features.Home.Views;
using BeninConnect.App.Features.Messaging.Components;
using BeninConnect.App.Features.Messaging.ViewModels;
using BeninConnect.App.Features.Messaging.Views;
using BeninConnect.App.Features.Onboarding.Views;
using BeninConnect.App.Features.Profile.ViewModels;
using BeninConnect.App.Features.Profile.Views;
using BeninConnect.App.Features.Settings.Views;
using BeninConnect.App.Shell;

namespace BeninConnect.UiTests;

/// <summary>Parcours de démonstration de bout en bout, sur les vraies vues (section 15.2 de la spécification).</summary>
public class DemoJourneyTests
{
    [AvaloniaFact]
    public async Task Complete_demo_journey()
    {
        using var ui = new UiDriver();

        // 1. Lancement → 2. Onboarding
        ui.Main.Start();
        await ui.WaitForAsync<OnboardingView>();
        await ui.ClickAsync("Suivant");
        await ui.ClickAsync("Suivant");
        await ui.ClickAsync("Commencer");

        // 3. Connexion mockée
        await ui.WaitForAsync<LoginView>();
        await ui.ClickAsync("Utiliser");
        await ui.ClickAsync("Se connecter");

        // 4. Accueil
        await ui.WaitForAsync<HomeView>();
        var firstCard = await ui.WaitForAsync<PostCard>();
        var post = (PostItemViewModel)firstCard.DataContext!;
        var likes = post.LikeCount;
        await ui.ClickAsync("J'aime");
        await ui.WaitUntilAsync(() => post.LikeCount != likes, "réaction mise à jour");

        // 5. Création d'une publication (onglet Publier)
        await ui.ClickAsync("Publier");
        await ui.WaitForAsync<CreatePostView>();
        await ui.TypeAsync("Partagez une expérience, un conseil, une bonne nouvelle…", "Publication créée par le test UI");
        var publish = ui.Window.GetVisualDescendants().OfType<Button>()
            .First(b => b.IsEffectivelyVisible && (b.Content as string) == "Publier" && b.Command is not null);
        publish.Command!.Execute(null);
        await ui.WaitForAsync<HomeView>();
        await ui.WaitForAsync<PostCard>(c => (c.DataContext as PostItemViewModel)?.Content == "Publication créée par le test UI");

        // 6. Adhésion à une communauté
        await ui.ClickAsync("Communautés");
        var card = await ui.WaitForAsync<CommunityCard>(c => (c.DataContext as CommunityItemViewModel)?.CanJoin == true);
        var community = (CommunityItemViewModel)card.DataContext!;
        community.JoinCommand.Execute(null);
        await ui.WaitUntilAsync(() => community.Status != App.Features.Communities.Models.MembershipStatus.None, "adhésion enregistrée");

        // 7. Envoi d'un message
        await ui.ClickAsync("Messages");
        await ui.WaitForAsync<ConversationsView>();
        var messagesTab = ((ShellViewModel)ui.Main.CurrentPage!).Messages;
        await ui.WaitUntilAsync(() => messagesTab.Conversations.Count > 0, "conversations chargées");
        messagesTab.Conversations[0].OpenCommand.Execute(null);
        await ui.WaitForAsync<ConversationView>();
        await ui.TypeAsync("Votre message…", "Message envoyé par le test UI");
        await ui.ClickAsync("Envoyer");
        await ui.WaitForAsync<MessageBubble>(b => (b.DataContext as MessageItemViewModel)?.Content == "Message envoyé par le test UI");
        ui.Get<INavigationService>().GoBack();

        // 8. Inscription à un événement
        await ui.ClickAsync("Accueil");
        await ui.ClickAsync("Tous les événements");
        await ui.WaitForAsync<EventsView>();
        var eventCard = await ui.WaitForAsync<EventCard>(c => (c.DataContext as App.Features.Events.ViewModels.EventItemViewModel) is { IsParticipating: false, IsFull: false });
        var ev = (App.Features.Events.ViewModels.EventItemViewModel)eventCard.DataContext!;
        ev.ToggleParticipationCommand.Execute(null);
        await ui.WaitUntilAsync(() => ev.IsParticipating, "inscription à l'événement");
        ui.Get<INavigationService>().GoBack();

        // 9. Modification du profil
        await ui.ClickAsync("Profil");
        await ui.WaitForAsync<ProfileView>(v => (v.DataContext as ProfileViewModel)?.IsContent == true);
        await ui.ClickAsync("Modifier le profil");
        await ui.WaitForAsync<EditProfileView>();
        var edit = (EditProfileViewModel)ui.Main.CurrentPage!;
        edit.City = "Cotonou";
        await ui.ClickAsync("Enregistrer");
        await ui.WaitUntilAsync(() => ui.Get<ISessionService>().CurrentUser?.City == "Cotonou", "profil enregistré");

        // 10. Déconnexion (Paramètres)
        await ui.ClickAsync("Paramètres");
        await ui.WaitForAsync<SettingsView>();
        await ui.ClickAsync("Se déconnecter");
        await ui.WaitUntilAsync(() => ui.Main.Dialogs.IsOpen, "dialogue de confirmation");
        ui.Main.Dialogs.Current!.ConfirmCommand.Execute(null);
        await ui.WaitForAsync<LoginView>();

        Assert.True(ui.BindingErrors.Errors.Count == 0, string.Join("\n", ui.BindingErrors.Errors.Distinct()));
    }

    [AvaloniaFact]
    public async Task Every_screen_renders_in_light_and_dark_theme_without_binding_errors()
    {
        using var ui = new UiDriver();
        var session = ui.Get<ISessionService>();
        var store = ui.Get<App.MockData.MockDataStore>();
        session.SignIn(store.Users.First(u => u.Id == store.DemoUserId));
        var nav = ui.Get<INavigationService>();

        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            Application.Current!.RequestedThemeVariant = variant;
            nav.ResetTo<ShellViewModel>();
            await ui.WaitForAsync<PostCard>();

            var shell = (ShellViewModel)nav.CurrentViewModel!;
            shell.ShowCommunitiesCommand.Execute(null);
            await ui.WaitForAsync<CommunityCard>();
            shell.ShowMessagesCommand.Execute(null);
            await ui.WaitForAsync<ConversationsView>();
            shell.ShowProfileCommand.Execute(null);
            await ui.WaitForAsync<ProfileView>(v => (v.DataContext as ProfileViewModel)?.IsContent == true);

            var post = store.Posts.First();
            var community = store.Communities.First(c => c.IsPrivate);
            var screens = new (Action Open, Type View)[]
            {
                (() => nav.NavigateTo<PostDetailsViewModel>(post.Id), typeof(PostDetailsView)),
                (() => nav.NavigateTo<CreatePostViewModel>(), typeof(CreatePostView)),
                (() => nav.NavigateTo<CommunityDetailsViewModel>(community.Id), typeof(App.Features.Communities.Views.CommunityDetailsView)),
                (() => nav.NavigateTo<CommunityMembersViewModel>(store.Communities[0].Id), typeof(App.Features.Communities.Views.CommunityMembersView)),
                (() => nav.NavigateTo<CreateCommunityViewModel>(), typeof(App.Features.Communities.Views.CreateCommunityView)),
                (() => nav.NavigateTo<ConversationViewModel>(store.Conversations[0].Id), typeof(ConversationView)),
                (() => nav.NavigateTo<NewConversationViewModel>(), typeof(NewConversationView)),
                (() => nav.NavigateTo<App.Features.Events.ViewModels.EventsViewModel>(), typeof(EventsView)),
                (() => nav.NavigateTo<App.Features.Events.ViewModels.EventDetailsViewModel>(store.Events[0].Id), typeof(EventDetailsView)),
                (() => nav.NavigateTo<App.Features.Events.ViewModels.CreateEventViewModel>(), typeof(CreateEventView)),
                (() => nav.NavigateTo<App.Features.Search.ViewModels.SearchViewModel>("Paris"), typeof(App.Features.Search.Views.SearchView)),
                (() => nav.NavigateTo<App.Features.Notifications.ViewModels.NotificationsViewModel>(), typeof(App.Features.Notifications.Views.NotificationsView)),
                (() => nav.NavigateTo<ProfileViewModel>(store.Users[5].Id), typeof(ProfileView)),
                (() => nav.NavigateTo<EditProfileViewModel>(), typeof(EditProfileView)),
                (() => nav.NavigateTo<App.Features.Settings.ViewModels.SettingsViewModel>(), typeof(SettingsView)),
                (() => nav.NavigateTo<App.Features.Authentication.ViewModels.RegisterViewModel>(), typeof(RegisterView)),
                (() => nav.NavigateTo<App.Features.Authentication.ViewModels.ForgotPasswordViewModel>(), typeof(ForgotPasswordView)),
                (() => nav.NavigateTo<App.Features.Authentication.ViewModels.VerifyEmailViewModel>("demo@beninconnect.test"), typeof(VerifyEmailView)),
            };

            foreach (var (open, viewType) in screens)
            {
                open();
                await ui.WaitUntilAsync(
                    () => ui.Window.GetVisualDescendants().Any(v => v.GetType() == viewType && ((Visual)v).IsEffectivelyVisible),
                    $"affichage de {viewType.Name} ({variant})");
                await nav.LastNavigationTask;
                ui.Pump();
                nav.GoBack();
            }
        }

        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        Assert.True(ui.BindingErrors.Errors.Count == 0, string.Join("\n", ui.BindingErrors.Errors.Distinct()));
    }
}
