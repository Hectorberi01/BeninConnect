using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BeninConnect.Api.Tests;

/// <summary>Lot A : carrousel, enregistrements, épingles, réponses, mentions, hashtags, partage en message.</summary>
[Collection(ApiCollection.Name)]
public sealed class SocialTests(ApiFactory factory)
{
    private sealed record TokenView(string Token);

    private sealed record IdView(Guid Id);

    private sealed record PostView(Guid Id, string? ImagePath, List<string> Images, List<Guid> MentionIds, DateTimeOffset? PinnedAt, Guid? PinnedCommentId);

    private sealed record CommentView(Guid Id, Guid? ParentId, List<Guid> MentionIds);

    private sealed record SaveView(Guid PostId, bool Saved);

    private sealed record NotificationView(string Title, Guid? TargetId);

    private sealed record MessageView(Guid Id, string Content, Guid? SharedPostId);

    private sealed record SummaryView(string? LastMessagePreview);

    private async Task<(HttpClient Http, Guid Id)> MemberAsync()
    {
        var http = factory.CreateClient();
        var email = $"social.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = "Afi", lastName = "Social", email, country = "Bénin", city = "Cotonou", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Secret123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenView>())!.Token);
        return (http, (await http.GetFromJsonAsync<IdView>("api/auth/me"))!.Id);
    }

    private static async Task<PostView> PostAsync(HttpClient http, object body)
    {
        var response = await http.PostAsJsonAsync("api/posts", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PostView>())!;
    }

    private static string Image() => $"uploads/{Guid.NewGuid():N}.jpg";

    private static async Task<List<NotificationView>> WaitForNotificationsAsync(HttpClient http, Func<List<NotificationView>, bool> done)
    {
        for (var i = 0; ; i++)
        {
            var list = (await http.GetFromJsonAsync<List<NotificationView>>("api/notifications"))!;
            if (done(list) || i == 40)
            {
                return list;
            }

            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task A_post_holds_up_to_ten_photos()
    {
        var (me, _) = await MemberAsync();
        var images = Enumerable.Range(0, 10).Select(_ => Image()).ToList();
        var post = await PostAsync(me, new { content = "Album", visibility = "Public", images });
        Assert.Equal(images, post.Images);
        Assert.Equal(images[0], post.ImagePath);

        var tooMany = await me.PostAsJsonAsync("api/posts", new { content = "Trop", visibility = "Public", images = images.Append(Image()) });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        var invalid = await me.PostAsJsonAsync("api/posts", new { content = "Faux", visibility = "Public", images = new[] { "../etc/passwd" } });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Saving_a_post_toggles_and_lists_it()
    {
        var (me, _) = await MemberAsync();
        var (other, _) = await MemberAsync();
        var post = await PostAsync(other, new { content = "À garder", visibility = "Public" });

        var saved = await (await me.PostAsync($"api/posts/{post.Id}/save", null)).Content.ReadFromJsonAsync<SaveView>();
        Assert.True(saved!.Saved);
        Assert.Equal([post.Id], await me.GetFromJsonAsync<List<Guid>>("api/posts/saved/ids"));
        Assert.Equal([post.Id], (await me.GetFromJsonAsync<List<IdView>>("api/posts/saved"))!.Select(p => p.Id));
        Assert.Empty((await other.GetFromJsonAsync<List<Guid>>("api/posts/saved/ids"))!);

        saved = await (await me.PostAsync($"api/posts/{post.Id}/save", null)).Content.ReadFromJsonAsync<SaveView>();
        Assert.False(saved!.Saved);
        Assert.Empty((await me.GetFromJsonAsync<List<Guid>>("api/posts/saved/ids"))!);
    }

    [Fact]
    public async Task Three_posts_can_be_pinned_and_they_come_first_on_the_profile()
    {
        var (me, meId) = await MemberAsync();
        var (other, _) = await MemberAsync();
        var posts = new List<PostView>();
        for (var i = 0; i < 5; i++)
        {
            posts.Add(await PostAsync(me, new { content = $"Publication {i}", visibility = "Public" }));
        }

        Assert.False((await other.PostAsync($"api/posts/{posts[0].Id}/pin", null)).IsSuccessStatusCode);
        foreach (var p in posts.Take(3))
        {
            (await me.PostAsync($"api/posts/{p.Id}/pin", null)).EnsureSuccessStatusCode();
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsync($"api/posts/{posts[3].Id}/pin", null)).StatusCode);

        var profile = (await me.GetFromJsonAsync<List<PostView>>($"api/posts?authorId={meId}"))!;
        Assert.Equal(posts.Take(3).Select(p => p.Id).ToHashSet(), profile.Take(3).Select(p => p.Id).ToHashSet());
        Assert.All(profile.Take(3), p => Assert.NotNull(p.PinnedAt));

        (await me.DeleteAsync($"api/posts/{posts[0].Id}/pin")).EnsureSuccessStatusCode();
        (await me.PostAsync($"api/posts/{posts[3].Id}/pin", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Replies_are_flattened_and_notify_the_comment_author()
    {
        var (author, _) = await MemberAsync();
        var (commenter, _) = await MemberAsync();
        var (replier, _) = await MemberAsync();
        var post = await PostAsync(author, new { content = "Discussion", visibility = "Public" });

        var root = (await (await commenter.PostAsJsonAsync($"api/posts/{post.Id}/comments", new { content = "Premier" }))
            .Content.ReadFromJsonAsync<CommentView>())!;
        var reply = (await (await replier.PostAsJsonAsync($"api/posts/{post.Id}/comments", new { content = "Réponse", parentId = root.Id }))
            .Content.ReadFromJsonAsync<CommentView>())!;
        var nested = (await (await commenter.PostAsJsonAsync($"api/posts/{post.Id}/comments", new { content = "Réponse à la réponse", parentId = reply.Id }))
            .Content.ReadFromJsonAsync<CommentView>())!;
        Assert.Equal(root.Id, reply.ParentId);
        Assert.Equal(root.Id, nested.ParentId);

        var unknown = await replier.PostAsJsonAsync($"api/posts/{post.Id}/comments", new { content = "Perdu", parentId = Guid.NewGuid() });
        Assert.True(unknown.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound);

        var notes = await WaitForNotificationsAsync(commenter, l => l.Any(n => n.Title.Contains("a répondu à votre commentaire")));
        Assert.Contains(notes, n => n.Title.Contains("a répondu à votre commentaire") && n.TargetId == post.Id);

        // Épingler un commentaire, puis le supprimer retire l'épingle et ses réponses
        var pinned = await (await author.PutAsJsonAsync($"api/posts/{post.Id}/pinned-comment", new { commentId = root.Id }))
            .Content.ReadFromJsonAsync<PostView>();
        Assert.Equal(root.Id, pinned!.PinnedCommentId);
        Assert.False((await commenter.PutAsJsonAsync($"api/posts/{post.Id}/pinned-comment", new { commentId = root.Id })).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Mentions_notify_members_and_hashtags_filter_posts()
    {
        var (me, _) = await MemberAsync();
        var (friend, friendId) = await MemberAsync();
        var tag = $"cotonou{Guid.NewGuid():N}"[..20];
        var post = await PostAsync(me, new { content = $"Fête ce soir avec @Afi Social #{tag} #Bénin", visibility = "Public", mentions = new[] { friendId, Guid.NewGuid() } });
        Assert.Equal([friendId], post.MentionIds);

        var notes = await WaitForNotificationsAsync(friend, l => l.Any(n => n.Title.Contains("mentionné")));
        Assert.Contains(notes, n => n.Title.Contains("vous a mentionné dans une publication") && n.TargetId == post.Id);

        (await me.PostAsJsonAsync($"api/posts/{post.Id}/comments", new { content = "Rappel @Afi", mentions = new[] { friendId } })).EnsureSuccessStatusCode();
        notes = await WaitForNotificationsAsync(friend, l => l.Any(n => n.Title.Contains("dans un commentaire")));
        Assert.Contains(notes, n => n.Title.Contains("vous a mentionné dans un commentaire"));

        var tagged = (await me.GetFromJsonAsync<List<IdView>>($"api/posts?tag={tag.ToUpperInvariant()}"))!;
        Assert.Equal([post.Id], tagged.Select(p => p.Id));
        Assert.Empty((await me.GetFromJsonAsync<List<IdView>>($"api/posts?tag=aucun{Guid.NewGuid():N}"))!);
    }

    [Fact]
    public async Task A_post_can_be_sent_in_a_conversation()
    {
        var (me, _) = await MemberAsync();
        var (friend, friendId) = await MemberAsync();
        var post = await PostAsync(friend, new { content = "Regarde ça", visibility = "Public" });
        var conversation = (await (await me.PostAsJsonAsync("api/conversations/direct", new { otherUserId = friendId }))
            .Content.ReadFromJsonAsync<IdView>())!;

        var sent = await me.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { postId = post.Id });
        sent.EnsureSuccessStatusCode();
        var message = (await sent.Content.ReadFromJsonAsync<MessageView>())!;
        Assert.Equal(post.Id, message.SharedPostId);

        var messages = (await friend.GetFromJsonAsync<List<MessageView>>($"api/conversations/{conversation.Id}/messages"))!;
        Assert.Contains(messages, m => m.SharedPostId == post.Id);

        var missing = await me.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { postId = Guid.NewGuid() });
        Assert.True(missing.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound);
        var empty = await me.PostAsJsonAsync($"api/conversations/{conversation.Id}/messages", new { content = "" });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task The_public_profile_page_has_open_graph_tags()
    {
        var (_, meId) = await MemberAsync();
        var http = factory.CreateClient();
        var page = await http.GetAsync($"user/{meId}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("og:title", html);
        Assert.Contains("Afi S.", html);
        Assert.Contains($"beninconnect://user/{meId}", html);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"user/{Guid.NewGuid()}")).StatusCode);
    }
}
