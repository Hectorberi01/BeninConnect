using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeninConnect.Modules.Notifications.Application;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PushTests(ApiFactory factory)
{
    private const string DemoToken = "ExponentPushToken[demo-push-test]";

    private sealed class FakePushSender : IPushSender
    {
        public ConcurrentQueue<PushMessage> Sent { get; } = new();

        public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct)
        {
            foreach (var m in messages)
            {
                Sent.Enqueue(m);
            }

            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    private sealed record TokenView(string Token);

    private sealed record IdView(Guid Id);

    private static async Task<HttpClient> SignInAsync(HttpClient http, string email, string password)
    {
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenView>())!.Token);
        return http;
    }

    [Fact]
    public async Task A_comment_is_pushed_to_the_author_phone_until_it_is_unregistered()
    {
        var fake = new FakePushSender();
        using var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IPushSender>(fake)));

        // Téléphone du compte de démonstration
        var demo = await SignInAsync(app.CreateClient(), "demo@beninconnect.test", "Demo123!");
        Assert.Equal(HttpStatusCode.NoContent, (await demo.PostAsJsonAsync("api/notifications/devices", new { token = DemoToken, platform = "ios" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await demo.PostAsJsonAsync("api/notifications/devices", new { token = "pas-un-jeton", platform = "ios" })).StatusCode);

        var me = (await demo.GetFromJsonAsync<IdView>("api/auth/me"))!;
        var post = (await demo.GetFromJsonAsync<List<IdView>>($"api/posts?authorId={me.Id}"))![0];

        // Un autre membre commente
        var email = $"push.{Guid.NewGuid():N}@exemple.test";
        var other = app.CreateClient();
        (await other.PostAsJsonAsync("api/auth/register",
            new { firstName = "Koffi", lastName = "Push", email, country = "Bénin", city = "Cotonou", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await other.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        await SignInAsync(other, email, "Secret123!");
        (await other.PostAsJsonAsync($"api/posts/{post.Id}/comments", new { content = "Bravo pour cette publication !" })).EnsureSuccessStatusCode();

        var message = await WaitForAsync(fake, m => m.Title.Contains("a commenté votre publication"));
        Assert.Equal(DemoToken, message.Token);
        Assert.Equal("Koffi Push a commenté votre publication", message.Title);
        Assert.Equal("Post", message.Data["target"]);
        Assert.Equal(post.Id.ToString(), message.Data["targetId"]);
        Assert.True(message.Badge >= 1);

        // Déconnexion du téléphone : plus de push
        Assert.Equal(HttpStatusCode.NoContent, (await demo.PostAsJsonAsync("api/notifications/devices/unregister", new { token = DemoToken })).StatusCode);
        var before = fake.Sent.Count;
        (await other.PostAsJsonAsync($"api/posts/{post.Id}/comments", new { content = "Encore bravo" })).EnsureSuccessStatusCode();
        await Task.Delay(500);
        Assert.Equal(before, fake.Sent.Count);
    }

    [Fact]
    public async Task Muted_categories_are_not_pushed()
    {
        var fake = new FakePushSender();
        using var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IPushSender>(fake)));
        var demo = await SignInAsync(app.CreateClient(), "demo@beninconnect.test", "Demo123!");
        await demo.PostAsJsonAsync("api/notifications/devices", new { token = "ExponentPushToken[muted]", platform = "android", mutedCategories = new[] { "Social" } });

        var me = (await demo.GetFromJsonAsync<IdView>("api/auth/me"))!;
        var post = (await demo.GetFromJsonAsync<List<IdView>>($"api/posts?authorId={me.Id}"))![0];
        var email = $"mute.{Guid.NewGuid():N}@exemple.test";
        var other = app.CreateClient();
        (await other.PostAsJsonAsync("api/auth/register",
            new { firstName = "Ayo", lastName = "Mute", email, country = "Bénin", city = "Porto-Novo", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await other.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        await SignInAsync(other, email, "Secret123!");
        (await other.PostAsJsonAsync($"api/posts/{post.Id}/comments", new { content = "Joli !" })).EnsureSuccessStatusCode();

        await Task.Delay(700);
        Assert.DoesNotContain(fake.Sent, m => m.Token == "ExponentPushToken[muted]");
        await demo.PostAsJsonAsync("api/notifications/devices/unregister", new { token = "ExponentPushToken[muted]" });
    }

    private static async Task<PushMessage> WaitForAsync(FakePushSender fake, Func<PushMessage, bool> match)
    {
        for (var i = 0; i < 50; i++)
        {
            if (fake.Sent.FirstOrDefault(match) is { } found)
            {
                return found;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Aucune notification push envoyée.");
    }
}
