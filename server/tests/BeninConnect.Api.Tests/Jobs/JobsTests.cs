using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BeninConnect.Modules.Jobs.Infrastructure.Sources;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Api.Tests.Jobs;

/// <summary>Faux serveurs France Travail et ReliefWeb : pages 206 puis 200, 429 puis succès, 401 puis nouveau jeton.</summary>
public sealed class FakeJobApis : HttpMessageHandler
{
    public string Tag { get; set; } = "x";
    public int FranceTravailTotal { get; set; } = 160;
    public ConcurrentQueue<string> Calls { get; } = new();
    public HashSet<string> Gone { get; } = [];
    public List<object> ReliefWebItems { get; } = [];
    public int Tokens;
    private int searches;
    private bool throttled;

    /// <summary>Nouveau scénario : 401 au premier appel, 429 sur la deuxième page.</summary>
    public void Reset()
    {
        searches = 0;
        throttled = false;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri!;
        Calls.Enqueue($"{request.Method} {url}");
        if (url.Host == "entreprise.francetravail.fr")
        {
            var form = await request.Content!.ReadAsStringAsync(ct);
            if (!form.Contains("client_secret=secret-ok"))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var n = Interlocked.Increment(ref Tokens);
            return Json(new { access_token = $"token-{n}", expires_in = 1499 });
        }

        if (url.Host == "api.francetravail.io")
        {
            if (url.AbsolutePath.EndsWith("/search", StringComparison.Ordinal))
            {
                // Premier appel : jeton refusé une fois
                if (Interlocked.Increment(ref searches) == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }

                var range = url.Query.Split('&').First(p => p.StartsWith("range=", StringComparison.Ordinal))[6..].Split('-').Select(int.Parse).ToArray();
                if (range[0] > 0 && !throttled)
                {
                    throttled = true;
                    var tooMany = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    tooMany.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                    return tooMany;
                }

                var end = Math.Min(range[1], FranceTravailTotal - 1);
                var items = Enumerable.Range(range[0], Math.Max(0, end - range[0] + 1)).Select(Offer).ToList();
                if (items.Count == 0)
                {
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }

                var partial = end < FranceTravailTotal - 1;
                var response = Json(new { resultats = items }, partial ? HttpStatusCode.PartialContent : HttpStatusCode.OK);
                response.Content.Headers.TryAddWithoutValidation("Content-Range", $"offres {range[0]}-{end}/{FranceTravailTotal}");
                return response;
            }

            var id = url.Segments[^1];
            return new HttpResponseMessage(Gone.Contains(id) ? HttpStatusCode.NoContent : HttpStatusCode.OK);
        }

        if (url.Host == "api.reliefweb.int")
        {
            return Json(new { totalCount = ReliefWebItems.Count, count = ReliefWebItems.Count, data = ReliefWebItems });
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    public object Offer(int i) => new
    {
        id = $"{Tag}-{i}",
        intitule = $"Développeur {Tag} {i}",
        description = i == 7 ? "Des frais de dossier de 50 € sont demandés." : "Poste en CDI.",
        dateCreation = DateTimeOffset.UtcNow.AddMinutes(-i).ToString("O"),
        lieuTravail = new { libelle = "75 - Paris", codePostal = "75011" },
        entreprise = new { nom = $"Acme {Tag}" },
        typeContrat = i % 2 == 0 ? "CDI" : "CDD",
        typeContratLibelle = "Contrat"
    };

    private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
}

public sealed class JobsFactory : ApiFactory
{
    public FakeJobApis Apis { get; } = new();

    protected override void Configure(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(s =>
        {
            foreach (var name in new[] { FranceTravailSource.ClientName, ReliefWebSource.ClientName, AdzunaSource.ClientName, RemotiveSource.ClientName })
            {
                s.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => Apis);
            }
        });
}

public sealed class JobsTests(JobsFactory factory) : IClassFixture<JobsFactory>
{
    private sealed record TokenBody(string Token);

    private sealed record Report(int Fetched, int Added, int Updated, int Duplicates, int Hidden, int Expired, List<string> Errors);

    private sealed record Summary(Guid Id, string Title, string ContractType, string SourceLabel, bool IsFavorite, string Status);

    private sealed record PageView(List<Summary> Items, string? NextCursor);

    private sealed record Detail(Guid Id, string Title, string ApplyUrl, string SourceLabel, bool IsFavorite, bool ReportedByMe);

    private sealed record SourceView(string Source, bool IsEnabled, string SecretState, string? LastError, int ActiveOffers);

    private sealed record Overview(List<SourceView> Sources, List<string> AlertKeywords, int ReportThreshold);

    private sealed record AdminOffer(Guid Id, string Title, string Status, string HiddenReason, bool NeedsReview, string? AlertKeyword, int ReportCount);

    private sealed record AdminPage(List<AdminOffer> Items, int TotalCount);

    private async Task<HttpClient> AdminAsync(string email = "superadmin@beninconnect.test")
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new { email, password = "Admin2026!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private async Task<HttpClient> MemberAsync()
    {
        var http = factory.CreateClient();
        var email = $"emploi.{Guid.NewGuid():N}@exemple.test";
        (await http.PostAsJsonAsync("api/auth/register",
            new { firstName = "Koffi", lastName = "Emploi", email, country = "France", city = "Paris", password = "Secret123!" })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("api/auth/verify-email", new { email, code = "123456" })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("api/auth/login", new { email, password = "Secret123!" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenBody>())!.Token);
        return http;
    }

    private static async Task<T> Ok<T>(Task<HttpResponseMessage> call)
    {
        var response = await call;
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpClient> ConfigureFranceTravailAsync()
    {
        factory.Apis.Reset();
        var admin = await AdminAsync();
        await Ok<SourceView>(admin.PutAsJsonAsync("api/admin/jobs/sources/FranceTravail/secret", new { secret = "" }));
        var refused = await admin.PutAsJsonAsync("api/admin/jobs/sources/FranceTravail",
            new { isEnabled = true, importIntervalMinutes = 120, clientId = "PAR_test", searches = new[] { new { key = "dev", keywords = "développeur" } } });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode); // secret manquant

        var source = await Ok<SourceView>(admin.PutAsJsonAsync("api/admin/jobs/sources/FranceTravail/secret", new { secret = "secret-ok" }));
        Assert.Equal("Console", source.SecretState);
        source = await Ok<SourceView>(admin.PutAsJsonAsync("api/admin/jobs/sources/FranceTravail",
            new { isEnabled = true, importIntervalMinutes = 120, clientId = "PAR_test", searches = new[] { new { key = "dev", keywords = "développeur" } } }));
        Assert.True(source.IsEnabled);
        return admin;
    }

    [Fact]
    public async Task France_travail_import_pages_retries_and_is_idempotent()
    {
        factory.Apis.Tag = $"ft{Guid.NewGuid():N}"[..10];
        factory.Apis.FranceTravailTotal = 160;
        var admin = await ConfigureFranceTravailAsync();
        var tokens = factory.Apis.Tokens;

        var first = await Ok<Report>(admin.PostAsync("api/admin/jobs/sources/FranceTravail/run", null));
        Assert.Empty(first.Errors);
        Assert.Equal(160, first.Fetched);
        Assert.Equal(160, first.Added);
        Assert.Equal(1, first.Hidden); // « frais de dossier »
        Assert.True(factory.Apis.Tokens > tokens); // 401 : jeton renouvelé

        var second = await Ok<Report>(admin.PostAsync("api/admin/jobs/sources/FranceTravail/run", null));
        Assert.Equal(0, second.Added);
        Assert.Equal(160, second.Updated);

        // Liste des membres : curseur, filtres, masquée exclue
        var member = await MemberAsync();
        var ids = new List<Guid>();
        string? cursor = null;
        do
        {
            var page = await member.GetFromJsonAsync<PageView>($"api/jobs?q={factory.Apis.Tag}&take=50&cursor={cursor}");
            ids.AddRange(page!.Items.Select(i => i.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(159, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        var filters = await member.GetFromJsonAsync<JsonElement>("api/jobs/filters");
        Assert.True(filters.GetProperty("total").GetInt32() >= 159);
        Assert.Contains(filters.GetProperty("departments").EnumerateArray(), d => d.GetProperty("label").GetString() == "75");
        Assert.Contains(filters.GetProperty("countries").EnumerateArray(), d => d.GetProperty("label").GetString() == "FR");
        var adminFilters = await admin.GetFromJsonAsync<JsonElement>("api/admin/jobs/preview/filters");
        Assert.Equal(filters.GetProperty("total").GetInt32(), adminFilters.GetProperty("total").GetInt32());
        var cdi = await member.GetFromJsonAsync<PageView>($"api/jobs?q={factory.Apis.Tag}&contract=Cdi&take=50");
        Assert.All(cdi!.Items, i => Assert.Equal("Cdi", i.ContractType));

        // File de la console : offre masquée par mot-clé d'alerte, puis réaffichée
        var review = await admin.GetFromJsonAsync<AdminPage>($"api/admin/jobs/offers?view=Review&q={factory.Apis.Tag}");
        var flagged = Assert.Single(review!.Items);
        Assert.Equal("frais de dossier", flagged.AlertKeyword);
        var shown = await Ok<AdminOffer>(admin.PostAsync($"api/admin/jobs/offers/{flagged.Id}/show", null));
        Assert.Equal("Active", shown.Status);
        Assert.False(shown.NeedsReview);
    }

    [Fact]
    public async Task Members_save_report_and_reports_hide_the_offer()
    {
        factory.Apis.Tag = $"fav{Guid.NewGuid():N}"[..10];
        factory.Apis.FranceTravailTotal = 5;
        var admin = await ConfigureFranceTravailAsync();
        await Ok<Report>(admin.PostAsync("api/admin/jobs/sources/FranceTravail/run", null));

        var member = await MemberAsync();
        var offer = (await member.GetFromJsonAsync<PageView>($"api/jobs?q={factory.Apis.Tag}"))!.Items[0];
        await Ok<JsonElement>(member.PostAsync($"api/jobs/{offer.Id}/favorite", null));
        var detail = await member.GetFromJsonAsync<Detail>($"api/jobs/{offer.Id}");
        Assert.True(detail!.IsFavorite);
        Assert.Equal("France Travail", detail.SourceLabel);
        Assert.Contains(offer.Id, (await member.GetFromJsonAsync<List<Summary>>("api/jobs/favorites"))!.Select(f => f.Id));
        await Ok<JsonElement>(member.DeleteAsync($"api/jobs/{offer.Id}/favorite"));
        Assert.DoesNotContain(offer.Id, (await member.GetFromJsonAsync<List<Summary>>("api/jobs/favorites"))!.Select(f => f.Id));

        Assert.Equal(HttpStatusCode.BadRequest, (await member.PostAsJsonAsync($"api/jobs/{offer.Id}/report", new { reason = "" })).StatusCode);
        for (var i = 0; i < 3; i++)
        {
            var reporter = i == 0 ? member : await MemberAsync();
            (await reporter.PostAsJsonAsync($"api/jobs/{offer.Id}/report", new { reason = "Demande d'argent" })).EnsureSuccessStatusCode();
            if (i == 0)
            {
                Assert.Equal(HttpStatusCode.Conflict, (await member.PostAsJsonAsync($"api/jobs/{offer.Id}/report", new { reason = "Encore" })).StatusCode);
            }
        }

        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"api/jobs/{offer.Id}")).StatusCode);
        var hidden = (await admin.GetFromJsonAsync<AdminPage>($"api/admin/jobs/offers?view=Review&q={factory.Apis.Tag}"))!.Items.Single(o => o.Id == offer.Id);
        Assert.Equal("Reports", hidden.HiddenReason);

        // Masquée définitivement : un réimport ne la réaffiche pas
        await Ok<AdminOffer>(admin.PostAsync($"api/admin/jobs/offers/{offer.Id}/hide", null));
        await Ok<Report>(admin.PostAsync("api/admin/jobs/sources/FranceTravail/run", null));
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"api/jobs/{offer.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_reliefweb_copy_of_a_france_travail_offer_is_a_hidden_duplicate()
    {
        factory.Apis.Tag = $"dup{Guid.NewGuid():N}"[..10];
        factory.Apis.FranceTravailTotal = 3;
        var admin = await ConfigureFranceTravailAsync();
        await Ok<Report>(admin.PostAsync("api/admin/jobs/sources/FranceTravail/run", null));

        factory.Apis.ReliefWebItems.Clear();
        factory.Apis.ReliefWebItems.Add(new
        {
            id = $"rw-{factory.Apis.Tag}",
            fields = new
            {
                title = $"Développeur {factory.Apis.Tag} 0",
                source = new[] { new { name = $"Acme {factory.Apis.Tag}" } },
                city = new[] { new { name = "75 - Paris" } },
                date = new { created = DateTimeOffset.UtcNow.ToString("O") },
                url_alias = "https://reliefweb.int/job/1"
            }
        });
        factory.Apis.ReliefWebItems.Add(new
        {
            id = $"rw2-{factory.Apis.Tag}",
            fields = new
            {
                title = $"Chargé de programme {factory.Apis.Tag}",
                source = new[] { new { name = "ONG Bénin" } },
                country = new[] { new { name = "Benin", iso3 = "BEN" } },
                date = new { created = DateTimeOffset.UtcNow.ToString("O"), closing = DateTimeOffset.UtcNow.AddDays(-1).ToString("O") },
                url_alias = "https://reliefweb.int/job/2"
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("api/admin/jobs/sources/ReliefWeb",
            new { isEnabled = true, importIntervalMinutes = 360, clientId = "", searches = new[] { new { key = "benin", country = "Benin" } } })).StatusCode);
        await Ok<SourceView>(admin.PutAsJsonAsync("api/admin/jobs/sources/ReliefWeb",
            new { isEnabled = true, importIntervalMinutes = 360, clientId = "beninconnect-test", searches = new[] { new { key = "benin", country = "Benin" } } }));
        var report = await Ok<Report>(admin.PostAsync("api/admin/jobs/sources/ReliefWeb/run", null));
        Assert.Equal(2, report.Added);
        Assert.Equal(1, report.Duplicates);
        Assert.Contains(factory.Apis.Calls, c => c.Contains("appname=beninconnect-test", StringComparison.Ordinal));

        var member = await MemberAsync();
        var list = (await member.GetFromJsonAsync<PageView>($"api/jobs?q={factory.Apis.Tag}"))!.Items;
        Assert.Equal(3, list.Count);
        Assert.All(list, i => Assert.Equal("France Travail", i.SourceLabel));

        // Aperçu de la console : la même liste que les membres
        var preview = (await admin.GetFromJsonAsync<PageView>($"api/admin/jobs/preview?q={factory.Apis.Tag}"))!.Items;
        Assert.Equal(list.Select(i => i.Id), preview.Select(i => i.Id));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"api/admin/jobs/preview/{list[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await AdminAsync("compta@beninconnect.test")).GetAsync("api/admin/jobs/preview")).StatusCode);

        // Offres importées : le doublon ReliefWeb est visible côté console, avec son offre d'origine
        var duplicate = Assert.Single((await admin.GetFromJsonAsync<AdminPage>($"api/admin/jobs/offers?view=Duplicates&q={factory.Apis.Tag}"))!.Items);
        var detail = await admin.GetFromJsonAsync<JsonElement>($"api/admin/jobs/offers/{duplicate.Id}");
        Assert.Equal($"Développeur {factory.Apis.Tag} 0", detail.GetProperty("duplicateOfTitle").GetString());
        Assert.Equal(3, (await admin.GetFromJsonAsync<AdminPage>($"api/admin/jobs/offers?view=Visible&q={factory.Apis.Tag}"))!.TotalCount);

        var overview = await admin.GetFromJsonAsync<Overview>("api/admin/jobs");
        Assert.Contains(overview!.Sources, s => s.Source == "ReliefWeb" && s.IsEnabled && s.LastError is null);
        Assert.Contains("frais de dossier", overview.AlertKeywords);
    }

    [Fact]
    public async Task Job_admin_is_restricted_and_validated()
    {
        var accountant = await AdminAsync("compta@beninconnect.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("api/admin/jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("api/jobs")).StatusCode);

        var admin = await AdminAsync();
        var remotive = await admin.PutAsJsonAsync("api/admin/jobs/sources/Remotive",
            new { isEnabled = true, importIntervalMinutes = 360, searches = new[] { new { key = "dev", category = "software-dev" } } });
        Assert.Equal(HttpStatusCode.BadRequest, remotive.StatusCode);

        var settings = await Ok<Overview>(admin.PutAsJsonAsync("api/admin/jobs/settings",
            new { alertKeywords = new[] { "frais de dossier", "visa garanti" }, reportThreshold = 2 }));
        Assert.Equal(2, settings.ReportThreshold);
        await Ok<Overview>(admin.PutAsJsonAsync("api/admin/jobs/settings",
            new { alertKeywords = new[] { "frais de dossier", "visa garanti", "western union" }, reportThreshold = 3 }));
    }
}
