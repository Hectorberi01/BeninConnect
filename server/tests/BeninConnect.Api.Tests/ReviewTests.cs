using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeninConnect.Shared.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Api.Tests;

/// <summary>Production : les comptes de revue sont renvoyés vers l'API de revue, sans rien vérifier ni créer.</summary>
public sealed class ReviewRedirectFactory : ApiFactory
{
    public const string ReviewEmail = "appreview@monbenin.com";

    protected override void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("Review:Emails:0", ReviewEmail);
        builder.UseSetting("Review:ApiUrl", "https://review-api.monbenin.test/");
    }
}

/// <summary>API en mode Review : protections de production, compte de revue, données de démo.</summary>
public sealed class ReviewModeFactory : ApiFactory
{
    public const string AccountEmail = "appreview@monbenin.com";
    public const string AccountPassword = "Revue-Apple-2026!";

    protected override void Configure(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Review");
        builder.UseSetting("Jwt:SigningKey", "cle-de-test-du-mode-revue-0123456789-abcdefghijklmnop");
        builder.UseSetting("Auth:FixedVerificationCode", "123456");
        builder.UseSetting("Database:AllowInsecure", "true");
        builder.UseSetting("Database:AllowLocalhost", "true");
        builder.UseSetting("Database:SeedDemoData", "true");
        builder.UseSetting("Backups:Automatic", "false");
        builder.UseSetting("Review:AccountEmail", AccountEmail);
        builder.UseSetting("Review:AccountPassword", AccountPassword);
        builder.UseSetting("Review:ResetDailyAtUtc", "");
        // Mode Review : même redirection configurée que la production, mais jamais appliquée ici
        builder.UseSetting("Review:Emails:0", AccountEmail);
        builder.UseSetting("Review:ApiUrl", "https://review-api.monbenin.test");
    }
}

public sealed class ReviewRedirectTests(ReviewRedirectFactory factory) : IClassFixture<ReviewRedirectFactory>
{
    private sealed record RedirectView(int Status, string Detail, string Redirect);

    private sealed record TokenView(string Token);

    [Theory]
    [InlineData("api/auth/login", ReviewRedirectFactory.ReviewEmail)]
    [InlineData("api/auth/login", "  AppReview@MonBenin.com ")]
    [InlineData("api/auth/register", ReviewRedirectFactory.ReviewEmail)]
    [InlineData("api/auth/forgot-password", ReviewRedirectFactory.ReviewEmail)]
    public async Task Review_accounts_are_sent_to_the_review_api(string path, string email)
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync(path, new
        {
            email, password = "peu-importe", firstName = "Apple", lastName = "Review", country = "France", city = "Paris"
        });

        Assert.Equal(HttpStatusCode.MisdirectedRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RedirectView>();
        Assert.Equal("https://review-api.monbenin.test", body!.Redirect);
        Assert.Contains("dernière version", body.Detail); // lu par les anciennes applications
    }

    [Fact]
    public async Task Registering_a_review_email_creates_nothing_in_production()
    {
        var http = factory.CreateClient();
        await http.PostAsJsonAsync("api/auth/register", new
        {
            firstName = "Apple", lastName = "Review", email = ReviewRedirectFactory.ReviewEmail, country = "France", city = "Paris", password = "Secret123!"
        });

        // Le code de vérification ne valide aucun compte : la demande est elle aussi redirigée
        var verify = await http.PostAsJsonAsync("api/auth/verify-email", new { email = ReviewRedirectFactory.ReviewEmail, code = "123456" });
        Assert.Equal(HttpStatusCode.MisdirectedRequest, verify.StatusCode);
    }

    [Fact]
    public async Task Other_members_sign_in_normally()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.False(string.IsNullOrEmpty((await login.Content.ReadFromJsonAsync<TokenView>())!.Token));
    }
}

public sealed class ReviewModeTests(ReviewModeFactory factory) : IClassFixture<ReviewModeFactory>
{
    private sealed record TokenView(string Token);

    private sealed record IdView(Guid Id);

    private async Task<HttpClient> ReviewerAsync()
    {
        var http = factory.CreateClient();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email = ReviewModeFactory.AccountEmail, password = ReviewModeFactory.AccountPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenView>())!.Token);
        return http;
    }

    [Fact]
    public async Task The_review_account_signs_in_with_demo_data_and_is_never_redirected()
    {
        var http = await ReviewerAsync();
        var feed = await http.GetAsync("api/feed?skip=0&take=10");
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
        Assert.Contains("\"id\"", await feed.Content.ReadAsStringAsync()); // fil de démonstration rempli
    }

    [Fact]
    public async Task No_public_demo_credentials_exist_in_review_mode()
    {
        var http = factory.CreateClient();
        var demo = await http.PostAsJsonAsync("api/auth/login", new { email = "demo@beninconnect.test", password = "Demo123!" });
        Assert.NotEqual(HttpStatusCode.OK, demo.StatusCode);
        var admin = await http.PostAsJsonAsync("api/admin/auth/login", new { email = "superadmin@beninconnect.test", password = "Admin2026!" });
        Assert.NotEqual(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    public async Task A_tester_can_register_with_the_fixed_code()
    {
        var http = factory.CreateClient();
        var email = $"testeur.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = "Test", lastName = "Apple", email, country = "France", city = "Paris", password = "Secret123!" })).EnsureSuccessStatusCode();
        var verify = await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    [Fact]
    public async Task Reset_erases_tester_content_and_keeps_the_review_account()
    {
        var http = await ReviewerAsync();
        var post = (await (await http.PostAsJsonAsync("api/posts", new { content = "Publication de test Apple", visibility = "Public" }))
            .Content.ReadFromJsonAsync<IdView>())!;

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDemoDataReset>().ResetAsync(CancellationToken.None);
        }

        var again = await ReviewerAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await again.GetAsync($"api/posts/{post.Id}")).StatusCode);
    }
}
