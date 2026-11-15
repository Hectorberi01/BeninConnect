using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeninConnect.Modules.Administration.Domain;

namespace BeninConnect.Api.Tests;

/// <summary>Bannières de l'accueil : une image 16:9 avec des boutons, gérée dans la console ; l'application ne voit que celles en ligne.</summary>
[Collection(ApiCollection.Name)]
public sealed class BannersTests(ApiFactory factory)
{
    private sealed record TokenBody(string Token);

    private sealed record ImageBody(string Path, int Width, int Height);

    private sealed record ActionView(string Label, string Icon, string Target);

    private sealed record BannerView(Guid Id, string Title, string ImagePath, string? Target, string Status, int Position, List<ActionView> Actions);

    private async Task<HttpClient> AdminAsync(string email = "admin@beninconnect.test")
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        response.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private async Task<HttpClient> MemberAsync()
    {
        var http = factory.CreateClient();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" });
        login.EnsureSuccessStatusCode();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return http;
    }

    /// <summary>En-tête PNG suffisant pour lire les dimensions.</summary>
    internal static byte[] Png(int width, int height)
    {
        var bytes = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
        BitConverter.GetBytes(width).Reverse().ToArray().CopyTo(bytes, 16);
        BitConverter.GetBytes(height).Reverse().ToArray().CopyTo(bytes, 20);
        return bytes;
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient admin, byte[] image)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "banniere.png");
        return await admin.PostAsync("api/admin/banners/images", form);
    }

    private static async Task<string> ImageAsync(HttpClient admin)
    {
        var response = await UploadAsync(admin, Png(1600, 900));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ImageBody>())!.Path;
    }

    private static object Banner(string title, string? image, bool active = true, DateTimeOffset? starts = null, DateTimeOffset? ends = null,
        object[]? actions = null, string? target = null) => new
    {
        title, imagePath = image, target, isActive = active, startsAt = starts, endsAt = ends,
        actions = actions ?? [new { label = "Voir les trajets", icon = "airplane", target = "voyages" }],
    };

    private static async Task<BannerView> CreateAsync(HttpClient admin, object body)
    {
        var response = await admin.PostAsJsonAsync("api/admin/banners", body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BannerView>())!;
    }

    [Fact]
    public async Task Only_16_9_images_large_enough_are_accepted()
    {
        var admin = await AdminAsync();
        var ok = (await (await UploadAsync(admin, Png(1920, 1080))).Content.ReadFromJsonAsync<ImageBody>())!;
        Assert.Equal((1920, 1080), (ok.Width, ok.Height));
        Assert.StartsWith("uploads/", ok.Path);

        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(admin, Png(1080, 1080))).StatusCode); // carré
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(admin, Png(800, 450))).StatusCode); // trop petite
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(admin, [1, 2, 3, 4])).StatusCode);
    }

    [Fact]
    public async Task Admin_manages_banners_and_the_app_only_sees_live_ones()
    {
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];
        var live = await CreateAsync(admin, Banner($"En ligne {tag}", await ImageAsync(admin), target: "https://monbenin.com/fete",
            actions: [new { label = "Communautés", icon = "account-group-outline", target = "communities" }, new { label = "Site", icon = "web", target = "https://monbenin.com" }]));
        var hidden = await CreateAsync(admin, Banner($"Masquée {tag}", await ImageAsync(admin), active: false));
        var later = await CreateAsync(admin, Banner($"Programmée {tag}", await ImageAsync(admin), starts: DateTimeOffset.UtcNow.AddDays(2)));
        var ended = await CreateAsync(admin, Banner($"Terminée {tag}", await ImageAsync(admin), starts: DateTimeOffset.UtcNow.AddDays(-3), ends: DateTimeOffset.UtcNow.AddDays(-1)));
        Assert.Equal(["Live", "Hidden", "Scheduled", "Ended"], new[] { live, hidden, later, ended }.Select(b => b.Status));

        var member = await MemberAsync();
        var seen = (await member.GetFromJsonAsync<List<BannerView>>("api/banners"))!;
        Assert.Contains(seen, b => b.Id == live.Id && b.Target == "https://monbenin.com/fete" && b.Actions.Count == 2 && b.ImagePath == live.ImagePath);
        Assert.DoesNotContain(seen, b => b.Id == hidden.Id || b.Id == later.Id || b.Id == ended.Id);

        // Masquée : elle disparaît de l'application
        var update = await admin.PutAsJsonAsync($"api/admin/banners/{live.Id}", Banner($"En ligne {tag}", live.ImagePath, active: false));
        Assert.Equal("Hidden", (await update.Content.ReadFromJsonAsync<BannerView>())!.Status);
        Assert.DoesNotContain((await member.GetFromJsonAsync<List<BannerView>>("api/banners"))!, b => b.Id == live.Id);

        // Ordre : la dernière passe en premier
        var all = (await admin.GetFromJsonAsync<List<BannerView>>("api/admin/banners"))!;
        var order = all.Select(b => b.Id).Where(id => id != ended.Id).Prepend(ended.Id).ToList();
        var reordered = (await (await admin.PutAsJsonAsync("api/admin/banners/order", new { ids = order })).Content.ReadFromJsonAsync<List<BannerView>>())!;
        Assert.Equal(ended.Id, reordered[0].Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("api/admin/banners/order", new { ids = order.Skip(1) })).StatusCode);

        foreach (var b in new[] { live, hidden, later, ended })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"api/admin/banners/{b.Id}")).StatusCode);
        }

        var audit = await admin.GetFromJsonAsync<JsonElement>($"api/admin/audit?targetId={live.Id}");
        Assert.Contains("Bannière supprimée", audit.GetRawText());
    }

    [Fact]
    public async Task Invalid_banners_are_refused_and_only_allowed_admins_manage_them()
    {
        var admin = await AdminAsync();
        var image = await ImageAsync(admin);
        foreach (var bad in new[]
                 {
                     Banner("ab", image),
                     Banner("Sans image", null),
                     Banner("Image inventée", "uploads/../../etc/passwd"),
                     Banner("Lien http", image, target: "http://exemple.com"),
                     Banner("Écran inconnu", image, actions: [new { label = "Écran", icon = "web", target = "admin.secret" }]),
                     Banner("Icône invalide", image, actions: [new { label = "Icône", icon = "<script>", target = "jobs" }]),
                     Banner("Dates inversées", image, starts: DateTimeOffset.UtcNow.AddDays(2), ends: DateTimeOffset.UtcNow.AddDays(1)),
                     Banner("Trop de boutons", image, actions: Enumerable.Range(0, 5).Select(i => (object)new { label = $"B{i}", icon = "web", target = "jobs" }).ToArray()),
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("api/admin/banners", bad)).StatusCode);
        }

        var moderator = await AdminAsync("moderation@beninconnect.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await moderator.GetAsync("api/admin/banners")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(moderator, Png(1600, 900))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("api/banners")).StatusCode);
    }

    [Fact]
    public void Image_size_is_read_from_jpeg_png_and_webp_headers()
    {
        Assert.Equal((1600, 900), ImageSize.Read(Png(1600, 900)));

        // JPEG : SOI, APP0 (16 octets), SOF0 avec hauteur puis largeur
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, .. new byte[14], 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x03, 0x84, 0x06, 0x40, 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.Equal((1600, 900), ImageSize.Read(jpeg));

        // WebP étendu (VP8X) : largeur - 1 et hauteur - 1 sur 3 octets
        var webp = new byte[30];
        "RIFF"u8.CopyTo(webp);
        "WEBP"u8.CopyTo(webp.AsSpan(8));
        "VP8X"u8.CopyTo(webp.AsSpan(12));
        (webp[24], webp[25], webp[26]) = (0x3F, 0x06, 0x00); // 1599
        (webp[27], webp[28], webp[29]) = (0x83, 0x03, 0x00); // 899
        Assert.Equal((1600, 900), ImageSize.Read(webp));

        Assert.Null(ImageSize.Read([1, 2, 3]));
        BannerRules.ValidateImageSize(1080, 608);
        Assert.Throws<Shared.Domain.DomainException>(() => BannerRules.ValidateImageSize(1200, 1200));
    }
}
