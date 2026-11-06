using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Feed.ViewModels;
using BeninConnect.App.Features.Home.ViewModels;
using BeninConnect.App.Shell;

namespace BeninConnect.Tests;

public class FeedTests
{
    private static async Task<HomeViewModel> OpenHomeAsync(TestHost host)
    {
        host.SignInDemo();
        var shell = await host.OpenAsync<ShellViewModel>();
        await shell.ActivationTask;
        return shell.Home;
    }

    [Fact]
    public async Task Home_loads_first_page_of_feed()
    {
        using var host = new TestHost();
        var home = await OpenHomeAsync(host);

        Assert.True(home.IsContent);
        Assert.Equal(HomeViewModel.PageSize, home.Posts.Count);
        Assert.True(home.HasMore);
        Assert.True(home.Posts.Zip(home.Posts.Skip(1)).All(p => p.First.CreatedAt >= p.Second.CreatedAt));
    }

    [Fact]
    public async Task Load_more_appends_next_page()
    {
        using var host = new TestHost();
        var home = await OpenHomeAsync(host);

        await home.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(HomeViewModel.PageSize * 2, home.Posts.Count);
        Assert.Equal(home.Posts.Count, home.Posts.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public async Task Publishing_adds_post_on_top_of_feed()
    {
        using var host = new TestHost();
        var home = await OpenHomeAsync(host);

        var create = await host.OpenAsync<CreatePostViewModel>();
        create.Content = "Bonjour à toute la communauté !";
        await create.PublishCommand.ExecuteAsync(null);

        Assert.Equal("Bonjour à toute la communauté !", home.Posts[0].Content);
        Assert.Contains(host.Store.Posts, p => p.Content == "Bonjour à toute la communauté !");
        Assert.IsType<ShellViewModel>(host.Navigation.CurrentViewModel);
    }

    [Fact]
    public async Task Like_toggles_reaction_and_updates_store()
    {
        using var host = new TestHost();
        var home = await OpenHomeAsync(host);
        var item = home.Posts.First(p => !p.IsLiked);
        var before = item.LikeCount;

        await item.ToggleLikeCommand.ExecuteAsync(null);

        Assert.True(item.IsLiked);
        Assert.Equal(before + 1, item.LikeCount);
        Assert.Equal(before + 1, host.Store.Posts.First(p => p.Id == item.Id).LikeCount);

        await item.ToggleLikeCommand.ExecuteAsync(null);
        Assert.False(item.IsLiked);
        Assert.Equal(before, item.LikeCount);
    }

    [Fact]
    public async Task Comment_increments_counter_everywhere()
    {
        using var host = new TestHost();
        var home = await OpenHomeAsync(host);
        var item = home.Posts[0];
        var before = item.CommentCount;

        var details = await host.OpenAsync<PostDetailsViewModel>(item.Id);
        var commentsBefore = details.Comments.Count;
        details.NewComment = "Très intéressant, merci !";
        await details.SendCommentCommand.ExecuteAsync(null);

        Assert.Equal(commentsBefore + 1, details.Comments.Count);
        Assert.Equal(string.Empty, details.NewComment);
        Assert.Equal(before + 1, item.CommentCount);
        Assert.Equal(before + 1, details.Post!.CommentCount);
    }

    [Fact]
    public async Task Share_increments_share_count()
    {
        using var host = new TestHost();
        var home = await OpenHomeAsync(host);
        var item = home.Posts[0];
        var before = item.ShareCount;

        await item.ShareCommand.ExecuteAsync(null);

        Assert.Equal(before + 1, item.ShareCount);
        Assert.NotNull(host.Toasts.LastMessage);
    }

    [Fact]
    public async Task Community_only_posts_are_hidden_from_non_members()
    {
        using var host = new TestHost();
        var me = host.SignInDemo();
        var repo = host.Get<BeninConnect.App.Features.Feed.Contracts.IPostRepository>();

        var feed = await repo.GetFeedAsync(me.Id, 0, 1000);
        var memberOf = host.Store.Communities.Where(c => c.MemberIds.Contains(me.Id)).Select(c => c.Id).ToHashSet();

        Assert.All(feed.Items.Where(p => p.Visibility == PostVisibility.CommunityMembers),
            p => Assert.Contains(p.CommunityId!.Value, memberOf));
    }
}
