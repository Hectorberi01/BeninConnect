using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BeninConnect.Modules.Jobs.Application;
using BeninConnect.Modules.Jobs.Domain;

namespace BeninConnect.Modules.Jobs.Infrastructure.Sources;

/// <summary>Adzuna (France). Palier gratuit limité : 2 pages de 50 offres par recherche.</summary>
public sealed class AdzunaSource(IHttpClientFactory factory, SourceHttp http) : IJobOfferSource
{
    public const string ClientName = "jobs-adzuna";
    public const string ApiUrl = "https://api.adzuna.com/v1/api/jobs/fr/search";

    private const int Page = 50;
    private const int MaxPages = 2;

    public JobSource Source => JobSource.Adzuna;

    public async IAsyncEnumerable<ImportedOfferData> FetchRecentAsync(JobSourceContext context, JobSearchDefinition search, int sinceDays,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var client = factory.CreateClient(ClientName);
        for (var page = 1; page <= MaxPages; page++)
        {
            var query = new List<string>
            {
                $"app_id={Uri.EscapeDataString(context.ClientId ?? string.Empty)}",
                $"app_key={Uri.EscapeDataString(context.Secret ?? string.Empty)}",
                $"results_per_page={Page}", $"max_days_old={sinceDays}", "sort_by=date", "content-type=application/json"
            };
            if (search.Keywords is { } what)
            {
                query.Add($"what={Uri.EscapeDataString(what)}");
            }

            if (search.Location is { } where)
            {
                query.Add($"where={Uri.EscapeDataString(where)}");
            }

            var url = $"{ApiUrl}/{page}?{string.Join('&', query)}";
            using var response = await http.SendAsync(client, () => new HttpRequestMessage(HttpMethod.Get, url), ct);
            using var doc = await SourceHttp.ReadJsonAsync(response, "Adzuna", ct);
            var count = 0;
            foreach (var item in doc.RootElement.Items("results"))
            {
                count++;
                if (Map(item) is { } offer)
                {
                    yield return offer;
                }
            }

            if (count < Page)
            {
                yield break;
            }
        }
    }

    /// <summary>Pas d'appel unitaire dans le palier gratuit : expiration par ancienneté.</summary>
    public Task<bool?> StillExistsAsync(JobSourceContext context, string externalId, CancellationToken ct) => Task.FromResult<bool?>(null);

    public static ImportedOfferData? Map(JsonElement o)
    {
        var id = o.Str("id");
        var title = o.Str("title");
        var url = o.Str("redirect_url");
        if (id is null || title is null || url is null)
        {
            return null;
        }

        var min = o.Num("salary_min");
        var max = o.Num("salary_max");
        var salary = (min, max) switch
        {
            ({ } a, { } b) when a > 0 && b > a => $"{a.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} à {b.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} € par an",
            ({ } a, _) when a > 0 => $"{a.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} € par an",
            _ => null
        };
        var contract = o.Str("contract_type");
        return new ImportedOfferData(
            id,
            JobText.PlainText(title, 300),
            JobText.PlainText(o.Str("description")),
            o.Str("company", "display_name"),
            null,
            "FR",
            o.Str("location", "display_name") ?? string.Empty,
            null,
            null,
            o.Num("latitude"),
            o.Num("longitude"),
            contract switch { "permanent" => ContractType.Cdi, "contract" => ContractType.Cdd, _ => ContractType.Autre },
            contract switch { "permanent" => "CDI", "contract" => "CDD", _ => null },
            false,
            o.Str("contract_time") switch { "full_time" => "Temps plein", "part_time" => "Temps partiel", _ => null },
            salary,
            o.Str("category", "label"),
            url,
            "Adzuna",
            o.Date("created") ?? DateTimeOffset.UtcNow,
            null);
    }
}
