using System.Runtime.CompilerServices;
using System.Text.Json;
using BeninConnect.Modules.Jobs.Application;
using BeninConnect.Modules.Jobs.Domain;

namespace BeninConnect.Modules.Jobs.Infrastructure.Sources;

/// <summary>Remotive (télétravail). Activation bloquée tant que les offres ne sont pas consultables sans compte (CGU Remotive).</summary>
public sealed class RemotiveSource(IHttpClientFactory factory, SourceHttp http) : IJobOfferSource
{
    public const string ClientName = "jobs-remotive";
    public const string ApiUrl = "https://remotive.com/api/remote-jobs";

    public JobSource Source => JobSource.Remotive;

    public async IAsyncEnumerable<ImportedOfferData> FetchRecentAsync(JobSourceContext context, JobSearchDefinition search, int sinceDays,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var client = factory.CreateClient(ClientName);
        var url = search.Category is { } category ? $"{ApiUrl}?category={Uri.EscapeDataString(category)}" : ApiUrl;
        if (search.Keywords is { } keywords)
        {
            url += (url.Contains('?') ? "&" : "?") + $"search={Uri.EscapeDataString(keywords)}";
        }

        using var response = await http.SendAsync(client, () => new HttpRequestMessage(HttpMethod.Get, url), ct);
        using var doc = await SourceHttp.ReadJsonAsync(response, "Remotive", ct);
        var since = DateTimeOffset.UtcNow.AddDays(-sinceDays);
        foreach (var item in doc.RootElement.Items("jobs"))
        {
            if (Map(item) is { } offer && offer.PublishedAt >= since)
            {
                yield return offer;
            }
        }
    }

    public Task<bool?> StillExistsAsync(JobSourceContext context, string externalId, CancellationToken ct) => Task.FromResult<bool?>(null);

    public static ImportedOfferData? Map(JsonElement o)
    {
        var id = o.Str("id");
        var title = o.Str("title");
        var url = o.Str("url");
        if (id is null || title is null || url is null)
        {
            return null;
        }

        var type = o.Str("job_type");
        return new ImportedOfferData(
            id, title, JobText.PlainText(o.Str("description")), o.Str("company_name"), o.Str("company_logo"),
            string.Empty, o.Str("candidate_required_location") ?? "Télétravail", null, null, null, null,
            type switch { "contract" or "freelance" => ContractType.Freelance, "internship" => ContractType.Stage, _ => ContractType.Autre },
            type, true, type switch { "full_time" => "Temps plein", "part_time" => "Temps partiel", _ => null }, o.Str("salary"),
            o.Str("category"), url, "Remotive", o.Date("publication_date") ?? DateTimeOffset.UtcNow, null);
    }
}
