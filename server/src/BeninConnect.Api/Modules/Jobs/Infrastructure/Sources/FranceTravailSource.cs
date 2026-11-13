using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using BeninConnect.Modules.Jobs.Application;
using BeninConnect.Modules.Jobs.Domain;

namespace BeninConnect.Modules.Jobs.Infrastructure.Sources;

/// <summary>Jetons OAuth2 France Travail en cache (par identifiant client), renouvelés avant expiration.</summary>
public sealed class FranceTravailTokens
{
    public ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> Cache { get; } = new();
}

/// <summary>France Travail — API Offres d'emploi v2 (France).</summary>
public sealed partial class FranceTravailSource(IHttpClientFactory factory, SourceHttp http, FranceTravailTokens tokens, TimeProvider clock) : IJobOfferSource
{
    public const string ClientName = "jobs-francetravail";
    public const string TokenUrl = "https://entreprise.francetravail.fr/connexion/oauth2/access_token?realm=%2Fpartenaire";
    public const string ApiUrl = "https://api.francetravail.io/partenaire/offresdemploi/v2/offres";
    public const string Scope = "api_offresdemploiv2 o2dsoffre";

    // Limites de l'API : 150 offres par page, début de plage ≤ 1000 (1 150 offres par recherche)
    private const int Page = 150;
    private const int MaxStart = 1000;
    private static readonly int[] PublishedSince = [1, 3, 7, 14, 31];

    public JobSource Source => JobSource.FranceTravail;

    public async IAsyncEnumerable<ImportedOfferData> FetchRecentAsync(JobSourceContext context, JobSearchDefinition search, int sinceDays,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var client = factory.CreateClient(ClientName);
        var since = PublishedSince.FirstOrDefault(d => d >= sinceDays, 31);
        for (var start = 0; start <= MaxStart; start += Page)
        {
            var url = $"{ApiUrl}/search?{Query(search, since)}&range={start}-{start + Page - 1}";
            using var response = await SendAsync(client, context, () => new HttpRequestMessage(HttpMethod.Get, url), ct);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                yield break;
            }

            using var doc = await SourceHttp.ReadJsonAsync(response, "France Travail", ct);
            var count = 0;
            foreach (var item in doc.RootElement.Items("resultats"))
            {
                count++;
                if (Map(item) is { } offer)
                {
                    yield return offer;
                }
            }

            // 200 : tout est arrivé ; 206 : suite selon Content-Range « offres 0-149/1234 »
            if (response.StatusCode != HttpStatusCode.PartialContent || count < Page || Total(response) is { } total && start + Page >= total)
            {
                yield break;
            }
        }
    }

    public async Task<bool?> StillExistsAsync(JobSourceContext context, string externalId, CancellationToken ct)
    {
        var client = factory.CreateClient(ClientName);
        using var response = await SendAsync(client, context,
            () => new HttpRequestMessage(HttpMethod.Get, $"{ApiUrl}/{Uri.EscapeDataString(externalId)}"), ct);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => true,
            HttpStatusCode.NoContent or HttpStatusCode.NotFound or HttpStatusCode.Gone => false,
            _ => null
        };
    }

    /// <summary>Jeton ajouté ; sur 401, jeton renouvelé puis un nouvel essai.</summary>
    private async Task<HttpResponseMessage> SendAsync(HttpClient client, JobSourceContext context, Func<HttpRequestMessage> build, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await TokenAsync(client, context, attempt > 0, ct);
            var response = await http.SendAsync(client, () =>
            {
                var request = build();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.ParseAdd("application/json");
                return request;
            }, ct);
            if (response.StatusCode != HttpStatusCode.Unauthorized || attempt > 0)
            {
                return response;
            }

            response.Dispose();
        }
    }

    private async Task<string> TokenAsync(HttpClient client, JobSourceContext context, bool renew, CancellationToken ct)
    {
        var clientId = context.ClientId ?? throw new InvalidOperationException("France Travail : identifiant client manquant.");
        var now = clock.GetUtcNow();
        if (!renew && tokens.Cache.TryGetValue(clientId, out var cached) && cached.ExpiresAt > now)
        {
            return cached.Token;
        }

        using var response = await http.SendAsync(client, () => new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = context.Secret ?? string.Empty,
                ["scope"] = Scope
            })
        }, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"France Travail : jeton refusé ({(int)response.StatusCode}). Vérifiez l'identifiant, le secret et l'abonnement à « Offres d'emploi v2 ».",
                null, response.StatusCode);
        }

        using var doc = await SourceHttp.ReadJsonAsync(response, "France Travail", ct);
        var token = doc.RootElement.Str("access_token") ?? throw new HttpRequestException("France Travail : réponse de jeton invalide.");
        var lifetime = doc.RootElement.Num("expires_in") ?? 1499;
        // Renouvelé une minute avant l'expiration
        tokens.Cache[clientId] = (token, now.AddSeconds(Math.Max(60, lifetime - 60)));
        return token;
    }

    private static string Query(JobSearchDefinition s, int since)
    {
        var p = new List<string> { $"publieeDepuis={since}", "sort=1" };
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                p.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
            }
        }

        Add("motsCles", s.Keywords);
        Add("typeContrat", s.ContractType);
        // Département prioritaire sur la région (l'API refuse les deux ensemble)
        if (!string.IsNullOrWhiteSpace(s.Department))
        {
            Add("departement", s.Department);
        }
        else
        {
            Add("region", s.Region);
        }

        return string.Join('&', p);
    }

    private static int? Total(HttpResponseMessage response) =>
        response.Content.Headers.TryGetValues("Content-Range", out var values) || response.Headers.TryGetValues("Content-Range", out values)
            ? RangeTotal().Match(values.First()) is { Success: true } m ? int.Parse(m.Groups[1].Value) : null
            : null;

    public static ImportedOfferData? Map(JsonElement o)
    {
        var id = o.Str("id");
        var title = o.Str("intitule");
        if (id is null || title is null)
        {
            return null;
        }

        var location = o.Str("lieuTravail", "libelle") ?? string.Empty;
        var contract = o.Str("typeContrat");
        var nature = o.Str("natureContrat") ?? string.Empty;
        var text = $"{title} {o.Str("description")}";
        return new ImportedOfferData(
            id,
            title,
            JobText.PlainText(o.Str("description")),
            o.Str("entreprise", "nom"),
            o.Str("entreprise", "logo"),
            "FR",
            location,
            o.Str("lieuTravail", "codePostal"),
            Department(location, o.Str("lieuTravail", "codePostal")),
            o.Num("lieuTravail", "latitude"),
            o.Num("lieuTravail", "longitude"),
            Contract(contract, nature, o.Bool("alternance") == true),
            o.Str("typeContratLibelle") ?? contract,
            Remote().IsMatch(text),
            o.Str("dureeTravailLibelleConverti") ?? o.Str("dureeTravailLibelle"),
            o.Str("salaire", "libelle") ?? o.Str("salaire", "commentaire"),
            o.Str("secteurActiviteLibelle"),
            o.Str("origineOffre", "urlOrigine") is { } origin && origin.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? origin
                : $"https://candidat.francetravail.fr/offres/recherche/detail/{Uri.EscapeDataString(id)}",
            "France Travail",
            o.Date("dateCreation") ?? o.Date("dateActualisation") ?? DateTimeOffset.UtcNow,
            null);
    }

    public static ContractType Contract(string? code, string nature, bool alternance)
    {
        if (alternance || nature.Contains("apprentissage", StringComparison.OrdinalIgnoreCase) ||
            nature.Contains("professionnalisation", StringComparison.OrdinalIgnoreCase))
        {
            return ContractType.Alternance;
        }

        return code?.ToUpperInvariant() switch
        {
            "CDI" => ContractType.Cdi,
            "CDD" or "SAI" => ContractType.Cdd,
            "MIS" => ContractType.Interim,
            "LIB" or "FRA" or "CCE" => ContractType.Freelance,
            _ => ContractType.Autre
        };
    }

    /// <summary>« 75 - Paris 11e » → 75 ; à défaut, deux premiers chiffres du code postal (97x pour l'outre-mer).</summary>
    private static string? Department(string label, string? postalCode)
    {
        var m = DepartmentPrefix().Match(label);
        if (m.Success)
        {
            return m.Groups[1].Value.ToUpperInvariant();
        }

        return postalCode is { Length: 5 } cp ? cp.StartsWith("97", StringComparison.Ordinal) ? cp[..3] : cp[..2] : null;
    }

    [GeneratedRegex(@"/(\d+)\s*$")]
    private static partial Regex RangeTotal();

    [GeneratedRegex(@"^\s*(\d{2,3}|2[AB])\s*-", RegexOptions.IgnoreCase)]
    private static partial Regex DepartmentPrefix();

    [GeneratedRegex(@"t[ée]l[ée]travail\s*(complet|total|100)|full\s*remote|100\s*%\s*(t[ée]l[ée]travail|remote)", RegexOptions.IgnoreCase)]
    private static partial Regex Remote();
}
