using System.Net.Http.Json;

namespace BeninConnect.Api.Tests;

/// <summary>La racine de l'API indique la version déployée (contrôle après un déploiement ou un retour arrière).</summary>
[Collection(ApiCollection.Name)]
public sealed class VersionTests(ApiFactory factory)
{
    private sealed record RootView(string Name, string Version);

    [Fact]
    public async Task Root_reports_the_running_version()
    {
        var root = await factory.CreateClient().GetFromJsonAsync<RootView>("/");
        Assert.Equal("BéninConnect API", root!.Name);
        Assert.Matches(@"^(\d+\.\d+\.\d+|sha-[0-9a-f]{7}|dev)", root.Version);
    }

    private sealed record AppVersionView(string? MinimumVersion);

    [Fact]
    public async Task The_mobile_app_learns_the_minimum_version()
    {
        var none = await factory.CreateClient().GetFromJsonAsync<AppVersionView>("api/app/version");
        Assert.Null(none!.MinimumVersion);

        using var configured = factory.WithWebHostBuilder(b => b.UseSetting("MobileApp:MinimumVersion", "1.1.1"));
        var response = await configured.CreateClient().GetAsync("api/app/version");
        Assert.Equal("1.1.1", (await response.Content.ReadFromJsonAsync<AppVersionView>())!.MinimumVersion);
        Assert.Contains("max-age=300", response.Headers.CacheControl!.ToString());
    }
}
