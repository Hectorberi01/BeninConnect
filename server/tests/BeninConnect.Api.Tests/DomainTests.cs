using BeninConnect.Modules.Communities.Domain;
using BeninConnect.Modules.Events.Domain;
using BeninConnect.Modules.Feed.Domain;
using BeninConnect.Modules.Messaging.Domain;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Api.Tests;

/// <summary>Règles métier pures (sans base de données).</summary>
public sealed class DomainTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Like_toggles()
    {
        var post = Post.Create(Guid.NewGuid(), Guid.NewGuid(), null, "Bonjour tout le monde", null, PostVisibility.Public, Now);
        var user = Guid.NewGuid();

        Assert.True(post.ToggleLike(user, Now));
        Assert.False(post.ToggleLike(user, Now));
        Assert.Empty(post.Likes);
    }

    [Fact]
    public void Community_only_post_requires_a_community() =>
        Assert.Throws<DomainException>(() => Post.Create(Guid.NewGuid(), Guid.NewGuid(), null, "Réservé aux membres", null, PostVisibility.CommunityMembers, Now));

    [Fact]
    public void Private_community_join_is_pending_and_owner_cannot_leave()
    {
        var owner = Guid.NewGuid();
        var community = Community.Create(Guid.NewGuid(), "Groupe privé", "Description du groupe privé", "Famille", "France", "Paris", CommunityVisibility.Private, null, owner, Now);
        var user = Guid.NewGuid();

        Assert.Equal(MembershipStatus.Pending, community.Join(user, Now));
        Assert.Equal(MembershipStatus.Pending, community.StatusFor(user));
        Assert.Throws<DomainException>(() => community.Leave(owner));
    }

    [Fact]
    public void Full_event_rejects_participants_but_allows_leaving()
    {
        var organizer = Guid.NewGuid();
        var ev = CommunityEvent.Create(Guid.NewGuid(), "Atelier", "Description suffisamment longue", "Formation", "Paris", "France", "Salle",
            Now.AddDays(2), Now.AddDays(2).AddHours(2), organizer, null, null, 2, Now);
        var first = Guid.NewGuid();

        Assert.True(ev.ToggleParticipation(first, Now));
        Assert.Throws<DomainException>(() => ev.ToggleParticipation(Guid.NewGuid(), Now));
        Assert.False(ev.ToggleParticipation(first, Now));
    }

    [Fact]
    public void Event_must_be_in_the_future() =>
        Assert.Throws<DomainException>(() => CommunityEvent.Create(Guid.NewGuid(), "Passé", "Description suffisamment longue", "Culture", "Paris", "", "Salle",
            Now.AddDays(-1), Now.AddDays(-1).AddHours(1), Guid.NewGuid(), null, null, null, Now));

    [Fact]
    public void Reaction_is_added_replaced_then_removed()
    {
        var post = Post.Create(Guid.NewGuid(), Guid.NewGuid(), null, "Bonjour tout le monde", null, PostVisibility.Public, Now);
        var comment = post.AddComment(Guid.NewGuid(), Guid.NewGuid(), "Super !", Now);
        var user = Guid.NewGuid();

        Assert.True(comment.React(user, "❤️", Now));
        Assert.True(comment.React(user, "😂", Now));
        Assert.Equal("😂", Assert.Single(comment.Reactions).Emoji);
        Assert.False(comment.React(user, "😂", Now));
        Assert.Empty(comment.Reactions);
    }

    [Fact]
    public void Unknown_reaction_is_rejected()
    {
        var message = Message.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Salut", Now);
        Assert.Throws<DomainException>(() => message.React(Guid.NewGuid(), "🍕", Now));
    }

    [Fact]
    public void A_message_can_only_be_edited_within_fifteen_minutes()
    {
        var sender = Guid.NewGuid();
        var message = Message.Create(Guid.NewGuid(), Guid.NewGuid(), sender, "Salut", Now);
        message.Edit(sender, "Salut à tous", Now.AddMinutes(14));
        Assert.Equal("Salut à tous", message.Content);
        Assert.NotNull(message.EditedAt);
        Assert.Throws<DomainException>(() => message.Edit(sender, "Trop tard", Now.AddMinutes(16)));
    }
}
