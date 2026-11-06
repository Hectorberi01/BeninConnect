using System.Net;
using System.Text;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Infrastructure.Api;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.Tests;

public class ApiClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static (ApiClient Client, StubHandler Handler) Create(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") }, new InMemoryPreferencesService(), new WeakReferenceMessenger());
        return (client, handler);
    }

    [Fact]
    public async Task Problem_details_become_readable_domain_errors()
    {
        var (client, _) = Create(HttpStatusCode.BadRequest, """{"title":"Requête refusée","detail":"Cet événement est complet."}""");

        var ex = await Assert.ThrowsAsync<DomainException>(() => client.GetAsync<CountDto>("api/x"));

        Assert.Equal("Cet événement est complet.", ex.Message);
        Assert.Equal("Cet événement est complet.", ErrorMessages.From(ex));
    }

    [Fact]
    public async Task Unauthorized_maps_to_session_expired()
    {
        var (client, _) = Create(HttpStatusCode.Unauthorized, "");
        await Assert.ThrowsAsync<ApiUnauthorizedException>(() => client.GetAsync<CountDto>("api/x"));
    }

    [Fact]
    public async Task Token_is_sent_and_enums_are_strings()
    {
        var (client, handler) = Create(HttpStatusCode.OK, """{"status":"Pending"}""");
        client.SetToken("abc");

        var result = await client.PostAsync<MembershipDto>("api/communities/1/join");

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal(App.Features.Communities.Models.MembershipStatus.Pending, result.Status);
    }

    [Fact]
    public async Task Logout_forgets_the_token()
    {
        var messenger = new WeakReferenceMessenger();
        var prefs = new InMemoryPreferencesService();
        var client = new ApiClient(new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")) { BaseAddress = new Uri("http://api.test/") }, prefs, messenger);
        client.SetToken("abc");

        messenger.Send(new SessionChangedMessage(null));

        Assert.Null(client.Token);
        Assert.Null(prefs.Get(PreferenceKeys.ApiToken));
        await Task.CompletedTask;
    }
}
