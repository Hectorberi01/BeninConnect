using System.Net;
using System.Net.Http.Json;
using BeninConnect.Host;
using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Identity.Infrastructure;
using BeninConnect.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BeninConnect.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ProductionTests(ApiFactory factory)
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "BeninConnect.Api";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void Migrations_are_up_to_date_with_the_model()
    {
        using var scope = factory.Services.CreateScope();
        var modules = scope.ServiceProvider.GetServices<ModuleDatabase>().ToList();
        Assert.Equal(10, modules.Count);
        foreach (var module in modules)
        {
            var db = (DbContext)scope.ServiceProvider.GetRequiredService(module.ContextType);
            Assert.NotEmpty(db.Database.GetMigrations());
            Assert.False(db.Database.HasPendingModelChanges(), $"Module {module.Module} modifié sans migration : tools/ef-migrations.sh <Nom>.");
        }
    }

    [Fact]
    public void Production_refuses_dangerous_settings()
    {
        var jwt = new JwtOptions { SigningKey = Production.DevSigningKey };
        var error = Assert.Throws<InvalidOperationException>(() => Production.Validate(Config(), new Env("Production"), jwt,
            new AuthSettings { FixedVerificationCode = "123456" }, new EmailSettings()));
        Assert.Contains("Jwt__SigningKey", error.Message);
        Assert.Contains("FixedVerificationCode", error.Message);
        Assert.Contains("Email__Provider", error.Message);
        Assert.Contains("SeedDemoData", error.Message);
        Assert.Contains("chiffrement obligatoire", error.Message);

        var local = Assert.Throws<InvalidOperationException>(() => Production.Validate(
            Config(("ConnectionStrings:Database", "Host=localhost;SSL Mode=Require")), new Env("Production"), jwt, new AuthSettings(), new EmailSettings()));
        Assert.Contains("hôte de la base hébergée", local.Message);

        Production.Validate(Config(("Database:SeedDemoData", "false"),
                ("ConnectionStrings:Database", "Host=ep-exemple.eu-central-1.aws.neon.tech;Database=beninconnect;Username=app;Password=x;SSL Mode=Require")), new Env("Production"),
            new JwtOptions { SigningKey = new string('k', 48) }, new AuthSettings(),
            new EmailSettings { Provider = "smtp", Host = "smtp.exemple.test", From = "no-reply@exemple.test" });
    }

    [Fact]
    public void Cloud_sql_unix_socket_is_accepted_without_ssl() =>
        Production.Validate(Config(("Database:SeedDemoData", "false"),
                ("ConnectionStrings:Database", "Host=/cloudsql/projet:europe-west1:beninconnect;Database=beninconnect;Username=app;Password=x")),
            new Env("Production"), new JwtOptions { SigningKey = new string('k', 48) }, new AuthSettings(),
            new EmailSettings { Provider = "smtp", Host = "smtp.exemple.test", From = "no-reply@exemple.test" });

    [Fact]
    public async Task Forgot_then_reset_password_with_emailed_code()
    {
        var http = factory.CreateClient();
        var email = $"reset.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register", new { firstName = "Rose", lastName = "Test", email, country = "France", city = "Paris", password = "Motdepasse1" }))
            .EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsJsonAsync("api/auth/forgot-password", new { email })).StatusCode);
        var wrong = await http.PostAsJsonAsync("api/auth/reset-password", new { email, code = "000000", newPassword = "Nouveau2026" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        // Code fixe de développement (en production : reçu par email).
        Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsJsonAsync("api/auth/reset-password", new { email, code = "123456", newPassword = "Nouveau2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("api/auth/login", new { email, password = "Nouveau2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("api/auth/reset-password", new { email, code = "123456", newPassword = "Encore2026x" })).StatusCode);

        // Pas d'énumération des comptes : même réponse pour une adresse inconnue.
        Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsJsonAsync("api/auth/forgot-password", new { email = "inconnu@exemple.test" })).StatusCode);
    }

    [Fact]
    public async Task Health_checks_the_database_and_responses_carry_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }
}
