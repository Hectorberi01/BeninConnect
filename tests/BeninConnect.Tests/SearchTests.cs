using BeninConnect.App.Features.Search.Services;
using BeninConnect.App.Features.Search.ViewModels;

namespace BeninConnect.Tests;

public class SearchTests
{
    [Fact]
    public async Task Search_finds_people_and_communities_by_city()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var vm = await host.OpenAsync<SearchViewModel>();
        vm.Debounce = TimeSpan.Zero;

        vm.Query = "Montréal";
        await vm.SearchAsync(debounce: false);

        Assert.True(vm.IsContent);
        Assert.True(vm.HasPeople);
        Assert.True(vm.HasCommunities);
        Assert.All(vm.People, p => Assert.Contains("Montréal", p.Subtitle));
    }

    [Fact]
    public async Task Search_ignores_accents()
    {
        using var host = new TestHost();
        var me = host.SignInDemo();
        var search = host.Get<ISearchService>();

        var withAccent = await search.SearchAsync("Béninois", SearchScope.Communities, me.Id);
        var withoutAccent = await search.SearchAsync("beninois", SearchScope.Communities, me.Id);

        Assert.NotEmpty(withAccent.Communities);
        Assert.Equal(withAccent.Communities.Count, withoutAccent.Communities.Count);
    }

    [Fact]
    public async Task Scope_filter_limits_result_types()
    {
        using var host = new TestHost();
        var me = host.SignInDemo();
        var search = host.Get<ISearchService>();

        var results = await search.SearchAsync("Paris", SearchScope.Events, me.Id);

        Assert.NotEmpty(results.Events);
        Assert.Empty(results.People);
        Assert.Empty(results.Posts);
        Assert.Empty(results.Communities);
    }

    [Fact]
    public async Task Unknown_query_shows_empty_state()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var vm = await host.OpenAsync<SearchViewModel>();
        vm.Debounce = TimeSpan.Zero;

        vm.Query = "zzzxxyy";
        await vm.SearchAsync(debounce: false);

        Assert.True(vm.IsEmpty);
    }
}
