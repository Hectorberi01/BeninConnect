using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Communities.ViewModels;
using BeninConnect.App.Shell;

namespace BeninConnect.Tests;

public class CommunityTests
{
    private static async Task<CommunitiesViewModel> OpenCommunitiesAsync(TestHost host)
    {
        host.SignInDemo();
        var shell = await host.OpenAsync<ShellViewModel>();
        shell.ShowCommunitiesCommand.Execute(null);
        await shell.ActivationTask;
        return shell.Communities;
    }

    [Fact]
    public async Task Lists_my_communities_and_recommendations()
    {
        using var host = new TestHost();
        var vm = await OpenCommunitiesAsync(host);

        Assert.NotEmpty(vm.MyCommunities);
        Assert.NotEmpty(vm.Recommended);
        Assert.All(vm.MyCommunities, c => Assert.True(c.IsMember));
        Assert.All(vm.Recommended, c => Assert.False(c.IsMember));
    }

    [Fact]
    public async Task Joining_public_community_moves_it_to_my_communities()
    {
        using var host = new TestHost();
        var vm = await OpenCommunitiesAsync(host);
        var target = vm.Recommended.First(c => !c.IsPrivate && c.CanJoin);
        var membersBefore = target.Community.MemberCount;

        await target.JoinCommand.ExecuteAsync(null);

        Assert.Equal(MembershipStatus.Member, target.Status);
        Assert.Contains(vm.MyCommunities, c => c.Id == target.Id);
        Assert.Equal(membersBefore + 1, host.Store.Communities.First(c => c.Id == target.Id).MemberCount);
        Assert.Contains(host.Store.Notifications, n => n.TargetId == target.Id && n.Title.StartsWith("Bienvenue"));
    }

    [Fact]
    public async Task Leaving_community_removes_membership()
    {
        using var host = new TestHost();
        var vm = await OpenCommunitiesAsync(host);
        var target = vm.MyCommunities.First(c => !c.IsOwner);

        await target.LeaveCommand.ExecuteAsync(null);

        Assert.Equal(MembershipStatus.None, target.Status);
        Assert.DoesNotContain(vm.MyCommunities, c => c.Id == target.Id);
        Assert.DoesNotContain(host.Store.DemoUserId, host.Store.Communities.First(c => c.Id == target.Id).MemberIds);
    }

    [Fact]
    public async Task Private_community_join_creates_pending_request_and_restricts_content()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var privateCommunity = host.Store.Communities.First(c => c.IsPrivate && !c.MemberIds.Contains(host.Store.DemoUserId));
        host.Store.JoinRequests.Clear();

        var details = await host.OpenAsync<CommunityDetailsViewModel>(privateCommunity.Id);
        Assert.True(details.IsRestricted);
        Assert.Empty(details.Posts);

        await details.Community!.JoinCommand.ExecuteAsync(null);
        Assert.Equal(MembershipStatus.Pending, details.Community.Status);
        Assert.Contains(host.Store.JoinRequests, r => r.CommunityId == privateCommunity.Id);
    }

    [Fact]
    public async Task Created_community_appears_immediately_in_list()
    {
        using var host = new TestHost();
        var list = await OpenCommunitiesAsync(host);

        var create = await host.OpenAsync<CreateCommunityViewModel>();
        create.Name = "Béninois de Nantes";
        create.Description = "Communauté locale des Béninois de Nantes et sa région.";
        create.Category = "Villes & régions";
        create.Country = "France";
        create.City = "Nantes";
        await create.CreateCommand.ExecuteAsync(null);

        Assert.Contains(list.MyCommunities, c => c.Name == "Béninois de Nantes" && c.IsOwner);
        Assert.IsType<CommunityDetailsViewModel>(host.Navigation.CurrentViewModel);
    }

    [Fact]
    public async Task Category_and_search_filters_apply()
    {
        using var host = new TestHost();
        var vm = await OpenCommunitiesAsync(host);

        vm.Categories.First(c => c.Key == "Tech").SelectCommand.Execute(null);
        Assert.All(vm.MyCommunities.Concat(vm.Recommended), c => Assert.Equal("Tech", c.Category));

        vm.ClearFiltersCommand.Execute(null);
        vm.SearchText = "Montréal";
        Assert.All(vm.MyCommunities.Concat(vm.Recommended), c =>
            Assert.True(c.Name.Contains("Montréal") || c.Description.Contains("Montréal") || c.Location.Contains("Montréal")));
    }
}
