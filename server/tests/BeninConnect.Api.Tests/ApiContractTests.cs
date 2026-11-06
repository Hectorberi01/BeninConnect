using System.Net;
using System.Net.Http.Json;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Authentication.Contracts;
using BeninConnect.App.Features.Communities.Models;
using BeninConnect.App.Features.Events.Models;
using BeninConnect.App.Features.Feed.Models;
using BeninConnect.App.Features.Messaging.Models;

namespace BeninConnect.Api.Tests;

/// <summary>
/// Scénarios de l'application mobile joués contre la vraie API (repositories HTTP du front → ASP.NET Core → PostgreSQL).
/// Les tests partagent la même base : ils vérifient des écarts plutôt que des valeurs absolues.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ApiContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Protected_endpoints_require_a_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/feed");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_with_readable_message()
    {
        var result = await factory.CreateFrontClient().Auth.LoginAsync("demo@beninconnect.test", "mauvais1");

        Assert.False(result.Succeeded);
        Assert.Equal("Email ou mot de passe incorrect.", result.Error);
    }

    [Fact]
    public async Task Demo_login_returns_profile_and_paged_feed()
    {
        var app = await factory.CreateFrontClient().LoginAsync();
        Assert.Equal("Sèna", app.Me.FirstName);

        var first = await app.Posts.GetFeedAsync(app.Me.Id, 0, 10);
        var second = await app.Posts.GetFeedAsync(app.Me.Id, 10, 10);

        Assert.Equal(10, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.True(first.TotalCount >= 90);
        Assert.Empty(first.Items.Select(p => p.Id).Intersect(second.Items.Select(p => p.Id)));
        Assert.True(first.Items.Zip(first.Items.Skip(1)).All(p => p.First.CreatedAt >= p.Second.CreatedAt));
    }

    [Fact]
    public async Task Create_like_and_comment_a_post()
    {
        var app = await factory.CreateFrontClient().LoginAsync();

        var post = await app.Posts.CreateAsync(new CreatePostRequest(app.Me.Id, "Publication envoyée par les tests d'intégration", null, null, PostVisibility.Public));
        Assert.Equal(app.Me.Id, post.AuthorId);

        await app.Posts.ToggleLikeAsync(post.Id, app.Me.Id);
        var liked = await app.Posts.GetByIdAsync(post.Id);
        Assert.Contains(app.Me.Id, liked!.LikedBy);

        await app.Comments.AddAsync(new CreateCommentRequest(post.Id, app.Me.Id, "Premier commentaire"));
        var commented = await app.Posts.GetByIdAsync(post.Id);
        Assert.Equal(1, commented!.CommentCount);
        Assert.Single(await app.Comments.GetForPostAsync(post.Id));

        var feed = await app.Posts.GetFeedAsync(app.Me.Id, 0, 1);
        Assert.Equal(post.Id, feed.Items[0].Id);
    }

    [Fact]
    public async Task Validation_errors_are_returned_as_readable_messages()
    {
        var app = await factory.CreateFrontClient().LoginAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => app.Communities.CreateAsync(
            new CreateCommunityRequest(app.Me.Id, "AB", "Trop court", "Tech", "France", "Paris", CommunityVisibility.Public, null)));

        Assert.Contains("au moins 3 caractères", ex.Message);
    }

    [Fact]
    public async Task Register_verify_and_login()
    {
        var app = factory.CreateFrontClient();
        var email = $"test-{Guid.NewGuid():N}@exemple.test";

        var registered = await app.Auth.RegisterAsync(new RegisterRequest("Awa", "Testeuse", email, "France", "Lyon", "Motdepasse1"));
        Assert.True(registered.Succeeded, registered.Error);

        var beforeVerification = await app.Auth.LoginAsync(email, "Motdepasse1");
        Assert.False(beforeVerification.Succeeded);

        var verified = await app.Auth.VerifyEmailAsync(email, "123456");
        Assert.True(verified.Succeeded, verified.Error);

        var login = await app.Auth.LoginAsync(email, "Motdepasse1");
        Assert.True(login.Succeeded, login.Error);

        var weak = await factory.CreateFrontClient().Auth.RegisterAsync(new RegisterRequest("Awa", "Testeuse", $"x{email}", "France", "Lyon", "court"));
        Assert.False(weak.Succeeded);
    }

    [Fact]
    public async Task Join_public_and_request_private_community()
    {
        var app = await factory.CreateFrontClient().LoginAsync("kossi.agossou@exemple.test");
        var all = await app.Communities.GetAllAsync();
        var pending = await app.Communities.GetPendingRequestsAsync(app.Me.Id);

        var publicOne = all.First(c => !c.IsPrivate && c.StatusFor(app.Me.Id, pending) == MembershipStatus.None);
        Assert.Equal(MembershipStatus.Member, await app.Communities.JoinAsync(publicOne.Id, app.Me.Id));
        Assert.Contains(app.Me.Id, (await app.Communities.GetByIdAsync(publicOne.Id))!.MemberIds);

        await app.Communities.LeaveAsync(publicOne.Id, app.Me.Id);
        Assert.DoesNotContain(app.Me.Id, (await app.Communities.GetByIdAsync(publicOne.Id))!.MemberIds);

        var privateOne = all.First(c => c.IsPrivate && c.StatusFor(app.Me.Id, pending) == MembershipStatus.None);
        Assert.Equal(MembershipStatus.Pending, await app.Communities.JoinAsync(privateOne.Id, app.Me.Id));
        await Assert.ThrowsAsync<DomainException>(() => app.Communities.GetMembersAsync(privateOne.Id));
        await app.Communities.CancelJoinRequestAsync(privateOne.Id, app.Me.Id);
    }

    [Fact]
    public async Task Messages_reach_the_other_member_with_unread_count_and_notification()
    {
        var sender = await factory.CreateFrontClient().LoginAsync();
        var receiver = await factory.CreateFrontClient().LoginAsync("kossi.agossou@exemple.test");
        var unreadBefore = await receiver.Conversations.GetUnreadCountAsync(receiver.Me.Id);
        var notificationsBefore = await receiver.Notifications.GetUnreadCountAsync(receiver.Me.Id);

        var conversation = await sender.Conversations.GetOrCreateDirectAsync(sender.Me.Id, receiver.Me.Id);
        await sender.Conversations.SendAsync(new SendMessageRequest(conversation.Id, sender.Me.Id, "Bonjour Kossi, test d'intégration !"));

        Assert.Equal(unreadBefore + 1, await receiver.Conversations.GetUnreadCountAsync(receiver.Me.Id));
        Assert.Equal(notificationsBefore + 1, await receiver.Notifications.GetUnreadCountAsync(receiver.Me.Id));

        var summary = (await receiver.Conversations.GetSummariesAsync(receiver.Me.Id)).First(s => s.Conversation.Id == conversation.Id);
        Assert.Equal("Bonjour Kossi, test d'intégration !", summary.LastMessage!.Content);

        await receiver.Conversations.MarkAsReadAsync(conversation.Id, receiver.Me.Id);
        Assert.Equal(unreadBefore, await receiver.Conversations.GetUnreadCountAsync(receiver.Me.Id));

        // Un tiers ne peut pas lire la conversation.
        var outsider = await factory.CreateFrontClient().LoginAsync("afi.dossou@exemple.test");
        await Assert.ThrowsAsync<DomainException>(() => outsider.Conversations.GetMessagesAsync(conversation.Id));
    }

    [Fact]
    public async Task Event_participation_and_full_events()
    {
        var app = await factory.CreateFrontClient().LoginAsync("afi.dossou@exemple.test");
        var events = await app.Events.GetUpcomingAsync(new EventFilter());
        Assert.NotEmpty(events);

        var open = events.First(e => !e.IsFull && !e.IsParticipating(app.Me.Id));
        Assert.True(await app.Events.ToggleParticipationAsync(open.Id, app.Me.Id));
        Assert.False(await app.Events.ToggleParticipationAsync(open.Id, app.Me.Id));

        var full = events.FirstOrDefault(e => e.IsFull && !e.IsParticipating(app.Me.Id));
        if (full is not null)
        {
            var ex = await Assert.ThrowsAsync<DomainException>(() => app.Events.ToggleParticipationAsync(full.Id, app.Me.Id));
            Assert.Equal("Cet événement est complet.", ex.Message);
        }

        var paris = await app.Events.GetUpcomingAsync(new EventFilter(City: "Paris"));
        Assert.All(paris, e => Assert.Equal("Paris", e.City));
    }

    [Fact]
    public async Task Notifications_can_be_marked_as_read()
    {
        var app = await factory.CreateFrontClient().LoginAsync("hermann.dansou@exemple.test");
        var demo = await factory.CreateFrontClient().LoginAsync();

        // Génère une notification pour Hermann (j'aime sur une de ses publications).
        var hisPost = (await demo.Posts.GetByAuthorAsync(app.Me.Id)).First();
        if (hisPost.IsLikedBy(demo.Me.Id))
        {
            await demo.Posts.ToggleLikeAsync(hisPost.Id, demo.Me.Id);
        }

        await demo.Posts.ToggleLikeAsync(hisPost.Id, demo.Me.Id);
        Assert.True(await app.Notifications.GetUnreadCountAsync(app.Me.Id) > 0);

        await app.Notifications.MarkAllAsReadAsync(app.Me.Id);
        Assert.Equal(0, await app.Notifications.GetUnreadCountAsync(app.Me.Id));
    }

    [Fact]
    public async Task Profile_update_is_persisted()
    {
        var app = await factory.CreateFrontClient().LoginAsync("rachida.yessoufou@exemple.test");

        var updated = await app.Users.UpdateProfileAsync(new App.Shared.Models.UpdateProfileRequest(
            app.Me.Id, "Rachida", "Yessoufou", "Professeure", "France", "Nice", "Bio mise à jour.", ["Études", "Littérature"]));

        Assert.Equal("Nice", updated.City);
        Assert.Equal("Nice", (await app.Users.GetByIdAsync(app.Me.Id))!.City);
    }

    [Fact]
    public async Task Search_finds_communities_and_members()
    {
        var app = await factory.CreateFrontClient().LoginAsync();

        Assert.Contains(await app.Communities.SearchAsync("montréal"), c => c.Name == "Béninois de Montréal");
        Assert.Contains(await app.Users.SearchAsync("Montréal"), u => u.City == "Montréal");
        Assert.NotEmpty(await app.Posts.SearchAsync("hiver", app.Me.Id));
    }

    private sealed record ReactionView(Guid UserId, string Emoji);

    private sealed record ReactedItem(Guid Id, IReadOnlyList<ReactionView> Reactions);

    [Fact]
    public async Task React_to_a_comment_notifies_its_author()
    {
        var author = await factory.CreateFrontClient().LoginAsync("kossi.agossou@exemple.test");
        var reader = await factory.CreateFrontClient().LoginAsync();
        var post = await author.Posts.CreateAsync(new CreatePostRequest(author.Me.Id, "Publication pour tester les réactions", null, null, PostVisibility.Public));
        await author.Comments.AddAsync(new CreateCommentRequest(post.Id, author.Me.Id, "Commentaire à faire réagir"));
        var commentId = (await author.Comments.GetForPostAsync(post.Id)).Single().Id;
        var before = await author.Notifications.GetUnreadCountAsync(author.Me.Id);

        var path = $"api/posts/{post.Id}/comments/{commentId}/reactions";
        var reacted = await reader.Api.PostAsync<ReactedItem>(path, new { emoji = "❤️" });
        Assert.Equal(new ReactionView(reader.Me.Id, "❤️"), Assert.Single(reacted.Reactions));
        Assert.Equal(before + 1, await author.Notifications.GetUnreadCountAsync(author.Me.Id));

        var removed = await reader.Api.PostAsync<ReactedItem>(path, new { emoji = "❤️" });
        Assert.Empty(removed.Reactions);
        await Assert.ThrowsAsync<DomainException>(() => reader.Api.PostAsync<ReactedItem>(path, new { emoji = "🍕" }));
    }

    [Fact]
    public async Task React_to_a_message_only_inside_the_conversation()
    {
        var sender = await factory.CreateFrontClient().LoginAsync();
        var receiver = await factory.CreateFrontClient().LoginAsync("ablawa.kpadonou@exemple.test");
        var conversation = await sender.Conversations.GetOrCreateDirectAsync(sender.Me.Id, receiver.Me.Id);
        await sender.Conversations.SendAsync(new SendMessageRequest(conversation.Id, sender.Me.Id, "Message à faire réagir"));
        var message = (await receiver.Conversations.GetMessagesAsync(conversation.Id)).Last();

        var path = $"api/conversations/{conversation.Id}/messages/{message.Id}/reactions";
        var reacted = await receiver.Api.PostAsync<ReactedItem>(path, new { emoji = "👍" });
        Assert.Equal("👍", Assert.Single(reacted.Reactions).Emoji);

        var outsider = await factory.CreateFrontClient().LoginAsync("afi.dossou@exemple.test");
        await Assert.ThrowsAsync<DomainException>(() => outsider.Api.PostAsync<ReactedItem>(path, new { emoji = "👍" }));
    }

    private sealed record TokenView(string Token);

    private sealed record MediaView(string Path);

    private sealed record ProfileView(string? AvatarImage, string? CoverImage);

    private async Task<HttpClient> RawClientAsync()
    {
        var http = factory.CreateClient();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" });
        var token = (await login.Content.ReadFromJsonAsync<TokenView>())!.Token;
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string name)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        return form;
    }

    [Fact]
    public async Task Upload_photo_then_use_it_as_avatar_and_cover()
    {
        var http = await RawClientAsync();
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0xFF, 0xD9];

        var response = await http.PostAsync("api/media", Upload(jpeg, "photo.jpg"));
        response.EnsureSuccessStatusCode();
        var media = (await response.Content.ReadFromJsonAsync<MediaView>())!;
        Assert.Matches("^uploads/[a-f0-9]{32}\\.jpg$", media.Path);
        Assert.Equal(jpeg, await http.GetByteArrayAsync(media.Path));

        var me = await http.GetFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>("api/auth/me");
        var update = new
        {
            firstName = me!["firstName"].GetString(), lastName = me["lastName"].GetString(), profession = "", country = "France", city = "Lyon",
            bio = "", interests = Array.Empty<string>(), avatarImage = (string?)media.Path, coverImage = (string?)media.Path,
        };
        var profile = await (await http.PutAsJsonAsync("api/users/me", update)).Content.ReadFromJsonAsync<ProfileView>();
        Assert.Equal(new ProfileView(media.Path, media.Path), profile);

        var removed = await (await http.PutAsJsonAsync("api/users/me", update with { avatarImage = "", coverImage = null }))
            .Content.ReadFromJsonAsync<ProfileView>();
        Assert.Equal(new ProfileView(null, media.Path), removed);
    }

    [Fact]
    public async Task Upload_rejects_non_images_and_foreign_paths()
    {
        var http = await RawClientAsync();
        var refused = await http.PostAsync("api/media", Upload("<script>alert(1)</script>"u8.ToArray(), "photo.jpg"));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, refused.StatusCode);

        var post = await http.PostAsJsonAsync("api/posts", new { content = "Image externe", imagePath = "https://exemple.test/x.jpg", visibility = "Public" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, post.StatusCode);
    }
}
