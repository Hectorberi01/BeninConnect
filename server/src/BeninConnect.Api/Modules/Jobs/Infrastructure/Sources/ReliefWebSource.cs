using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BeninConnect.Modules.Jobs.Application;
using BeninConnect.Modules.Jobs.Domain;

namespace BeninConnect.Modules.Jobs.Infrastructure.Sources;

/// <summary>ReliefWeb — API v2 jobs (ONG, ONU, humanitaire ; Bénin et Afrique de l'Ouest). Appname approuvé obligatoire.</summary>
public sealed class ReliefWebSource(IHttpClientFactory factory, SourceHttp http) : IJobOfferSource
{
    public const string ClientName = "jobs-reliefweb";
    public const string ApiUrl = "https://api.reliefweb.int/v2/jobs";

    private const int Page = 100;
    private const int MaxOffers = 1000;

    private static readonly string[] Fields =
    [
        "title", "body-html", "source.name", "source.shortname", "city.name", "country.name", "country.iso3", "date.created",
        "date.closing", "url_alias", "url", "type.name", "career_categories.name"
    ];

    public JobSource Source => JobSource.ReliefWeb;

    public async IAsyncEnumerable<ImportedOfferData> FetchRecentAsync(JobSourceContext context, JobSearchDefinition search, int sinceDays,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var client = factory.CreateClient(ClientName);
        var from = DateTimeOffset.UtcNow.AddDays(-sinceDays).ToString("yyyy-MM-ddT00:00:00+00:00");
        var conditions = new List<object> { new { field = "date.created", value = new { from } } };
        if (search.Country is { } country)
        {
            conditions.Add(new { field = "country.name", value = country });
        }

        if (search.Keywords is { } keywords)
        {
            conditions.Add(new { field = "title", value = keywords });
        }

        for (var offset = 0; offset < MaxOffers; offset += Page)
        {
            var body = new
            {
                offset,
                limit = Page,
                filter = new { @operator = "AND", conditions },
                sort = new[] { "date.created:desc" },
                fields = new { include = Fields }
            };
            using var response = await http.SendAsync(client,
                () => new HttpRequestMessage(HttpMethod.Post, Url(context)) { Content = JsonContent.Create(body) }, ct);
            using var doc = await SourceHttp.ReadJsonAsync(response, "ReliefWeb", ct);
            var count = 0;
            foreach (var item in doc.RootElement.Items("data"))
            {
                count++;
                if (Map(item) is { } offer)
                {
                    yield return offer;
                }
            }

            var total = (int?)doc.RootElement.Num("totalCount") ?? 0;
            if (count < Page || offset + Page >= total)
            {
                yield break;
            }
        }
    }

    /// <summary>Offre absente ou clôturée : retirée.</summary>
    public async Task<bool?> StillExistsAsync(JobSourceContext context, string externalId, CancellationToken ct)
    {
        var client = factory.CreateClient(ClientName);
        var url = $"{ApiUrl}/{Uri.EscapeDataString(externalId)}?appname={Uri.EscapeDataString(AppName(context))}&fields[include][]=date.closing";
        using var response = await http.SendAsync(client, () => new HttpRequestMessage(HttpMethod.Get, url), ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = await SourceHttp.ReadJsonAsync(response, "ReliefWeb", ct);
        var item = doc.RootElement.Items("data").Cast<JsonElement?>().FirstOrDefault();
        if (item is null)
        {
            return false;
        }

        return item.Value.Date("fields", "date", "closing") is not { } closing || closing > DateTimeOffset.UtcNow;
    }

    private static string AppName(JobSourceContext context) =>
        context.ClientId ?? throw new InvalidOperationException("ReliefWeb : appname manquant.");

    private static string Url(JobSourceContext context) => $"{ApiUrl}?appname={Uri.EscapeDataString(AppName(context))}";

    public static ImportedOfferData? Map(JsonElement item)
    {
        var id = item.Str("id");
        var f = item.Get("fields");
        if (id is null || f is not { } fields || fields.Str("title") is not { } title)
        {
            return null;
        }

        var organization = fields.First("source") is { } s ? s.Str("name") ?? s.Str("shortname") : null;
        var city = fields.First("city")?.Str("name");
        var country = fields.First("country");
        var countryName = country?.Str("name");
        var type = fields.First("type")?.Str("name");
        var url = fields.Str("url_alias") ?? fields.Str("url") ?? $"https://reliefweb.int/job/{id}";
        return new ImportedOfferData(
            id,
            title,
            JobText.PlainText(fields.Str("body-html") ?? fields.Str("body")),
            organization,
            null,
            Countries.FromIso3(country?.Str("iso3")),
            string.Join(", ", new[] { city, countryName }.Where(v => !string.IsNullOrWhiteSpace(v))),
            null,
            null,
            null,
            null,
            Contract(type),
            type,
            false,
            null,
            null,
            fields.First("career_categories")?.Str("name"),
            url,
            organization is null ? "ReliefWeb" : $"ReliefWeb · {organization}",
            fields.Date("date", "created") ?? DateTimeOffset.UtcNow,
            fields.Date("date", "closing"));
    }

    public static ContractType Contract(string? type) => type?.ToLowerInvariant() switch
    {
        { } t when t.Contains("intern") => ContractType.Stage,
        { } t when t.Contains("consult") => ContractType.Freelance,
        _ => ContractType.Autre
    };
}
