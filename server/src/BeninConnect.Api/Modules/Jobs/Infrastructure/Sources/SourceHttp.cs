using System.Net;
using System.Text.Json;
using BeninConnect.Modules.Jobs.Application;

namespace BeninConnect.Modules.Jobs.Infrastructure.Sources;

/// <summary>Appels HTTP des sources : pause entre appels, 429/503 réessayés après Retry-After (3 fois au plus).</summary>
public sealed class SourceHttp(JobsOptions options, TimeProvider clock)
{
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(60);

    public async Task<HttpResponseMessage> SendAsync(HttpClient http, Func<HttpRequestMessage> build, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (options.RequestDelayMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(options.RequestDelayMs), clock, ct);
            }

            var response = await http.SendAsync(build(), HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable) || attempt >= options.MaxRetries)
            {
                return response;
            }

            var wait = response.Headers.RetryAfter?.Delta
                       ?? (response.Headers.RetryAfter?.Date is { } date ? date - clock.GetUtcNow() : (TimeSpan?)null)
                       ?? TimeSpan.FromSeconds(2 << attempt);
            response.Dispose();
            await Task.Delay(wait < TimeSpan.Zero ? TimeSpan.Zero : wait > MaxWait ? MaxWait : wait, clock, ct);
        }
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, string source, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{source} : réponse {(int)response.StatusCode} {response.ReasonPhrase}", null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}

/// <summary>Lecture tolérante du JSON des sources (champ absent ou de mauvais type : null).</summary>
internal static class Json
{
    public static JsonElement? Get(this JsonElement e, params string[] path)
    {
        var current = e;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
            {
                return null;
            }
        }

        return current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : current;
    }

    public static string? Str(this JsonElement e, params string[] path) => e.Get(path) switch
    {
        { ValueKind: JsonValueKind.String } v => v.GetString(),
        { ValueKind: JsonValueKind.Number } v => v.GetRawText(),
        _ => null
    };

    public static double? Num(this JsonElement e, params string[] path) => e.Get(path) switch
    {
        { ValueKind: JsonValueKind.Number } v => v.GetDouble(),
        { ValueKind: JsonValueKind.String } v when double.TryParse(v.GetString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) => d,
        _ => null
    };

    public static bool? Bool(this JsonElement e, params string[] path) => e.Get(path) switch
    {
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.False } => false,
        _ => null
    };

    public static DateTimeOffset? Date(this JsonElement e, params string[] path) =>
        DateTimeOffset.TryParse(e.Str(path), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d.ToUniversalTime() : null;

    public static IEnumerable<JsonElement> Items(this JsonElement e, params string[] path) =>
        e.Get(path) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray() : [];

    /// <summary>Premier élément d'un tableau (ReliefWeb : source, ville, pays sont des tableaux).</summary>
    public static JsonElement? First(this JsonElement e, params string[] path) =>
        e.Get(path) switch
        {
            { ValueKind: JsonValueKind.Array } a => a.EnumerateArray().Cast<JsonElement?>().FirstOrDefault(),
            { ValueKind: JsonValueKind.Object } o => o,
            _ => null
        };
}

/// <summary>Codes pays ISO 3 → ISO 2 des pays suivis (Afrique de l'Ouest et voisins).</summary>
internal static class Countries
{
    private static readonly Dictionary<string, string> Iso3 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BEN"] = "BJ", ["TGO"] = "TG", ["NGA"] = "NG", ["NER"] = "NE", ["BFA"] = "BF", ["GHA"] = "GH", ["CIV"] = "CI", ["SEN"] = "SN",
        ["MLI"] = "ML", ["GIN"] = "GN", ["GNB"] = "GW", ["SLE"] = "SL", ["LBR"] = "LR", ["GMB"] = "GM", ["MRT"] = "MR", ["CPV"] = "CV",
        ["CMR"] = "CM", ["TCD"] = "TD", ["CAF"] = "CF", ["GAB"] = "GA", ["COG"] = "CG", ["COD"] = "CD", ["FRA"] = "FR", ["BEL"] = "BE",
        ["CHE"] = "CH", ["CAN"] = "CA", ["USA"] = "US", ["GBR"] = "GB", ["MAR"] = "MA", ["TUN"] = "TN", ["DZA"] = "DZ", ["KEN"] = "KE"
    };

    public static string FromIso3(string? iso3) => iso3 is not null && Iso3.TryGetValue(iso3, out var iso2) ? iso2 : string.Empty;
}
