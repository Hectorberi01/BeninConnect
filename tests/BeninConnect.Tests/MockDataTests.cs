using BeninConnect.App.MockData.Factories;

namespace BeninConnect.Tests;

public class MockDataTests
{
    [Fact]
    public void Seed_has_the_expected_volumes()
    {
        var store = MockDataFactory.Create(DateTimeOffset.Now);

        Assert.Equal(40, store.Users.Count);
        Assert.Equal(15, store.Communities.Count);
        Assert.Equal(100, store.Posts.Count);
        Assert.Equal(200, store.Comments.Count);
        Assert.Equal(12, store.Conversations.Count);
        Assert.Equal(150, store.Messages.Count);
        Assert.Equal(25, store.Events.Count);
        Assert.Equal(30, store.Notifications.Count);
    }

    [Fact]
    public void Seed_is_deterministic()
    {
        var now = DateTimeOffset.Now;
        var a = MockDataFactory.Create(now);
        var b = MockDataFactory.Create(now);

        Assert.Equal(a.Users.Select(u => u.Id), b.Users.Select(u => u.Id));
        Assert.Equal(a.Posts.Select(p => p.Id), b.Posts.Select(p => p.Id));
    }

    [Fact]
    public void Seed_is_consistent()
    {
        var store = MockDataFactory.Create(DateTimeOffset.Now);
        var userIds = store.Users.Select(u => u.Id).ToHashSet();

        Assert.All(store.Posts, p => Assert.Contains(p.AuthorId, userIds));
        Assert.All(store.Posts, p => Assert.Equal(store.Comments.Count(c => c.PostId == p.Id), p.CommentCount));
        Assert.All(store.Messages, m => Assert.Contains(store.Conversations, c => c.Id == m.ConversationId));
        Assert.All(store.Events, e => Assert.True(e.EndsAt > DateTimeOffset.Now));
        Assert.All(store.Communities, c => Assert.Contains(c.OwnerId, c.MemberIds));
        Assert.Contains(store.Users, u => u.Email == "demo@beninconnect.test");
        Assert.All(store.Posts.Where(p => p.CommunityId is not null), p =>
            Assert.Contains(p.AuthorId, store.Communities.First(c => c.Id == p.CommunityId).MemberIds));
    }
}
