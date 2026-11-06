using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Events.Contracts;
using BeninConnect.App.Features.Events.Models;
using BeninConnect.App.Features.Events.ViewModels;

namespace BeninConnect.Tests;

public class EventTests
{
    [Fact]
    public async Task Participation_toggles_registration()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var events = await host.OpenAsync<EventsViewModel>();
        var item = events.Events.First(e => !e.IsParticipating && !e.IsFull);
        var before = item.Event.ParticipantCount;

        await item.ToggleParticipationCommand.ExecuteAsync(null);

        Assert.True(item.IsParticipating);
        Assert.Equal(before + 1, item.Event.ParticipantCount);
        Assert.Contains(host.Store.DemoUserId, host.Store.Events.First(e => e.Id == item.Id).ParticipantIds);

        await item.ToggleParticipationCommand.ExecuteAsync(null);
        Assert.False(item.IsParticipating);
    }

    [Fact]
    public async Task Full_event_rejects_new_participants()
    {
        using var host = new TestHost();
        var me = host.SignInDemo();
        var repo = host.Get<IEventRepository>();
        var full = host.Store.Events.First(e => e.IsFull && !e.IsParticipating(me.Id));

        await Assert.ThrowsAsync<DomainException>(() => repo.ToggleParticipationAsync(full.Id, me.Id));
    }

    [Fact]
    public async Task City_filter_restricts_results()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var vm = await host.OpenAsync<EventsViewModel>();
        var total = vm.Events.Count;

        vm.SelectedCity = "Paris";
        await Task.Yield();

        Assert.NotEmpty(vm.Events);
        Assert.True(vm.Events.Count < total);
        Assert.All(vm.Events, e => Assert.Equal("Paris", e.City));
    }

    [Fact]
    public async Task Created_event_is_published_and_organizer_participates()
    {
        using var host = new TestHost();
        var me = host.SignInDemo();
        var vm = await host.OpenAsync<CreateEventViewModel>();
        vm.Title = "Rencontre des nouveaux arrivants";
        vm.Description = "Une soirée pour accueillir les membres arrivés récemment.";
        vm.Category = "Entraide";
        vm.Address = "Maison des associations (démo)";
        vm.Date = DateTimeOffset.Now.AddDays(10);

        await vm.PublishCommand.ExecuteAsync(null);

        var created = Assert.Single(host.Store.Events, e => e.Title == "Rencontre des nouveaux arrivants");
        Assert.True(created.IsParticipating(me.Id));
        Assert.IsType<EventDetailsViewModel>(host.Navigation.CurrentViewModel);
    }

    [Fact]
    public async Task Period_filter_this_week_only_returns_close_events()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var repo = host.Get<IEventRepository>();

        var week = await repo.GetUpcomingAsync(new EventFilter(Period: EventPeriod.ThisWeek));

        Assert.NotEmpty(week);
        Assert.All(week, e => Assert.True(e.StartsAt.Date < DateTimeOffset.Now.Date.AddDays(7)));
    }
}
