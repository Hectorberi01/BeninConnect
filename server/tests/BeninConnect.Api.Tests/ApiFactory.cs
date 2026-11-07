using BeninConnect.App.Core.Services;
using BeninConnect.App.Features.Authentication.Contracts;
using BeninConnect.App.Infrastructure.Api;
using BeninConnect.App.Shared.Models;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace BeninConnect.Api.Tests;

/// <summary>
/// API en mémoire branchée sur un PostgreSQL éphémère (conteneur Docker), seedé une fois.
/// Sans Docker : BC_TEST_POSTGRES=« Host=...;Username=...;Password=... » (une base neuve est créée par fabrique).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string? ExternalServer = Environment.GetEnvironmentVariable("BC_TEST_POSTGRES");

    private readonly PostgreSqlContainer? _db = ExternalServer is null
        ? new PostgreSqlBuilder().WithImage("postgres:17-alpine").WithDatabase("beninconnect_tests").Build()
        : null;

    private readonly string _externalDatabase = $"bc_tests_{Guid.NewGuid():N}";

    private string ConnectionString => _db?.GetConnectionString() ?? $"{ExternalServer};Database={_externalDatabase}";

    public async Task InitializeAsync()
    {
        if (_db is not null)
        {
            await _db.StartAsync();
        }

        await StartDependenciesAsync();
        _ = Server; // démarre l'hôte : création du schéma + jeu de démonstration
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Database", ConnectionString);
        builder.UseSetting("Media:Root", Path.Combine(Path.GetTempPath(), "beninconnect-tests-media"));
        builder.UseSetting("RateLimiting:Enabled", "false");
        builder.UseSetting("Push:Provider", "log"); // jamais de vrai push depuis les tests
        Configure(builder);
    }

    /// <summary>Services supplémentaires (ex. stockage S3) démarrés avant l'API.</summary>
    protected virtual Task StartDependenciesAsync() => Task.CompletedTask;

    protected virtual void Configure(IWebHostBuilder builder)
    {
    }

    protected virtual Task StopDependenciesAsync() => Task.CompletedTask;

    /// <summary>Client « application mobile » : repositories HTTP du front + session.</summary>
    public FrontClient CreateFrontClient() => new(CreateClient());

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await StopDependenciesAsync();
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}

public sealed class FrontClient
{
    public FrontClient(HttpClient http)
    {
        Messenger = new WeakReferenceMessenger();
        Api = new ApiClient(http, new InMemoryPreferencesService(), Messenger);
        Auth = new ApiAuthService(Api);
        Users = new ApiUserRepository(Api, Messenger);
        Posts = new ApiPostRepository(Api, Messenger);
        Comments = new ApiCommentRepository(Api, Messenger);
        Communities = new ApiCommunityRepository(Api, Messenger);
        Conversations = new ApiConversationRepository(Api, Messenger);
        Events = new ApiEventRepository(Api, Messenger);
        Notifications = new ApiNotificationRepository(Api, Messenger);
    }

    public IMessenger Messenger { get; }
    public ApiClient Api { get; }
    public ApiAuthService Auth { get; }
    public ApiUserRepository Users { get; }
    public ApiPostRepository Posts { get; }
    public ApiCommentRepository Comments { get; }
    public ApiCommunityRepository Communities { get; }
    public ApiConversationRepository Conversations { get; }
    public ApiEventRepository Events { get; }
    public ApiNotificationRepository Notifications { get; }

    public User Me { get; private set; } = null!;

    public async Task<FrontClient> LoginAsync(string email = "demo@beninconnect.test", string password = "Demo123!")
    {
        var result = await Auth.LoginAsync(email, password);
        Assert.True(result.Succeeded, result.Error);
        Me = result.User!;
        return this;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
