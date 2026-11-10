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
}
