using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Notifications.ViewModels;
using BeninConnect.App.Features.Profile.ViewModels;
using BeninConnect.App.Features.Settings.ViewModels;
using BeninConnect.App.Shell;

namespace BeninConnect.Tests;

public class ProfileAndNotificationTests
{
    [Fact]
    public async Task Editing_profile_updates_session_store_and_profile_tab()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var shell = await host.OpenAsync<ShellViewModel>();
        shell.ShowProfileCommand.Execute(null);
        await shell.ActivationTask;

        var edit = await host.OpenAsync<EditProfileViewModel>();
        edit.City = "Bordeaux";
        edit.Bio = "Nouvelle biographie de démonstration.";
        edit.Interests.First(i => !i.IsSelected).SelectCommand.Execute(null);
        await edit.SaveCommand.ExecuteAsync(null);

        var session = host.Get<ISessionService>();
        Assert.Equal("Bordeaux", session.CurrentUser!.City);
        Assert.Equal("Bordeaux", host.Store.Users.First(u => u.Id == session.CurrentUser.Id).City);
        Assert.Equal("Nouvelle biographie de démonstration.", shell.Profile.Bio);
        Assert.Contains("Bordeaux", shell.Profile.Location);
        Assert.IsType<ShellViewModel>(host.Navigation.CurrentViewModel);
    }

    [Fact]
    public async Task Edit_profile_rejects_empty_name()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var edit = await host.OpenAsync<EditProfileViewModel>();
        edit.FirstName = "";

        await edit.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(edit.FirstNameError);
        Assert.Same(edit, host.Navigation.CurrentViewModel);
    }

    [Fact]
    public async Task Other_user_profile_offers_messaging()
    {
        using var host = new TestHost();
        var me = host.SignInDemo();
        var other = host.Store.Users.First(u => u.Id != me.Id);

        var profile = await host.OpenAsync<ProfileViewModel>(other.Id);

        Assert.True(profile.IsOtherUser);
        Assert.Equal(other.DisplayName, profile.DisplayName);
        await profile.SendMessageCommand.ExecuteAsync(null);
        Assert.IsType<App.Features.Messaging.ViewModels.ConversationViewModel>(host.Navigation.CurrentViewModel);
    }

    [Fact]
    public async Task Mark_as_read_and_mark_all()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var vm = await host.OpenAsync<NotificationsViewModel>();
        Assert.Equal(30, vm.Items.Count);
        var unread = vm.UnreadCount;
        Assert.True(unread > 1);

        var first = vm.Items.First(i => i.IsUnread);
        await first.MarkReadCommand.ExecuteAsync(null);
        Assert.True(first.IsRead);
        Assert.Equal(unread - 1, vm.UnreadCount);

        await vm.MarkAllAsReadCommand.ExecuteAsync(null);
        Assert.Equal(0, vm.UnreadCount);
        Assert.All(vm.Items, i => Assert.True(i.IsRead));
        Assert.All(host.Store.Notifications.Where(n => n.UserId == host.Store.DemoUserId), n => Assert.True(n.IsRead));
    }

    [Fact]
    public async Task Opening_notification_navigates_to_its_content()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var vm = await host.OpenAsync<NotificationsViewModel>();
        var item = vm.Items.First(i => i.Notification.Target == App.Features.Notifications.Models.NotificationTarget.Event);

        await item.OpenCommand.ExecuteAsync(null);

        Assert.True(item.IsRead);
        Assert.IsType<App.Features.Events.ViewModels.EventDetailsViewModel>(host.Navigation.CurrentViewModel);
    }

    [Fact]
    public async Task Category_filter_shows_only_that_category()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var vm = await host.OpenAsync<NotificationsViewModel>();

        vm.Categories.First(c => c.Label == "Messages").SelectCommand.Execute(null);

        Assert.NotEmpty(vm.Items);
        Assert.All(vm.Items, i => Assert.True(i.IsMessage));
    }

    [Fact]
    public async Task Theme_choice_is_persisted()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var settings = await host.OpenAsync<SettingsViewModel>();

        settings.IsDarkTheme = true;

        Assert.Equal(AppTheme.Dark, host.Get<IThemeService>().Current);
        Assert.Equal("Dark", host.Get<IPreferencesService>().Get(PreferenceKeys.Theme));
    }
}
