using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeninConnect.Modules.Feed.Contracts;
using BeninConnect.Modules.Identity.Contracts;
using BeninConnect.Modules.Notifications.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Api.Tests;

/// <summary>Publication du compte officiel : tous les membres notifiés par vagues, un seul push par jour.</summary>
[Collection(ApiCollection.Name)]
public sealed class OfficialPostNotificationTests(ApiFactory factory)
{
    private async Task<HttpClient> NewMemberAsync()
    {
        var email = $"test.{Guid.NewGuid():N}@exemple.test";
        var member = factory.CreateClient();
        (await member.PostAsJsonAsync("api/auth/register", new { firstName = "Notif", lastName = "Test", email, country = "France", city = "Lyon", password = "Motdepasse1" }))
            .EnsureSuccessStatusCode();
        var body = await (await member.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>();
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return member;
    }

    private async Task<Guid> PublishAsync(string content)
    {
        using var scope = factory.Services.CreateScope();
        var official = await scope.ServiceProvider.GetRequiredService<IIdentityModule>().GetOfficialAccountAsync(CancellationToken.None);
        return await scope.ServiceProvider.GetRequiredService<IFeedModule>().PublishAsAsync(official, content, null, [], CancellationToken.None);
    }

    /// <summary>Vagues de 2 membres jusqu'à la fin des diffusions en attente.</summary>
    private async Task<int> DeliverAllAsync()
    {
        var (total, waves) = (0, 0);
        int sent;
        do
        {
            using var scope = factory.Services.CreateScope();
            sent = await scope.ServiceProvider.GetRequiredService<Broadcasts>().SendWaveAsync(2, CancellationToken.None);
            total += sent;
            waves++;
        }
        while (sent > 0 && waves < 10_000);

        return total;
    }

    private static JsonElement? NotificationFor(JsonElement list, Guid postId) =>
        list.EnumerateArray().Cast<JsonElement?>().SingleOrDefault(n => n!.Value.GetProperty("targetId").GetString() == postId.ToString());

    [Fact]
    public async Task Every_member_is_notified_in_waves_with_one_push_a_day()
    {
        var member = await NewMemberAsync();
        var first = await PublishAsync("La fête de l'indépendance approche : retrouvez le programme des célébrations.");
        var second = await PublishAsync("Deuxième annonce du jour.");

        // Rien tant que la tâche n'est pas passée
        Assert.Null(NotificationFor(await member.GetFromJsonAsync<JsonElement>("api/notifications"), first));
        Assert.True(await DeliverAllAsync() >= 2);

        var notifications = await member.GetFromJsonAsync<JsonElement>("api/notifications");
        var notification = NotificationFor(notifications, first)!.Value;
        Assert.Equal("BéninConnect a publié", notification.GetProperty("title").GetString());
        Assert.Equal(("System", "Post"), (notification.GetProperty("category").GetString(), notification.GetProperty("target").GetString()));
        Assert.StartsWith("La fête de l'indépendance", notification.GetProperty("body").GetString());
        Assert.NotNull(NotificationFor(notifications, second));

        // Push pour la première diffusion du jour seulement
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<INotificationsDb>();
        var broadcasts = await db.Broadcasts.Where(b => b.TargetId == first || b.TargetId == second).ToListAsync();
        Assert.All(broadcasts, b => Assert.NotNull(b.CompletedAt));
        Assert.False(broadcasts.Single(b => b.TargetId == second).SendPush);
    }

    [Fact]
    public void The_day_starts_at_midnight_in_Paris()
    {
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 23, 0, 0, TimeSpan.Zero), Broadcasts.StartOfDay(new DateTimeOffset(2026, 11, 17, 18, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 22, 0, 0, TimeSpan.Zero), Broadcasts.StartOfDay(new DateTimeOffset(2026, 7, 14, 21, 0, 0, TimeSpan.Zero)));
    }
}
