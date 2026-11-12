using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BeninConnect.Modules.Editorial.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeninConnect.Modules.Editorial.Application;

public interface IEditorialDb
{
    DbSet<ContentCandidate> Candidates { get; }

    DbSet<EditorialSource> Sources { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Article d'un flux RSS / Atom (lien absolu).</summary>
public sealed record FeedItem(string Title, string Url, string Summary);

public interface IFeedReader
{
    Task<IReadOnlyList<FeedItem>> ReadAsync(string url, int max, CancellationToken ct);
}

/// <summary>Fournisseur de rédaction (Claude). Non configuré : les articles attendent une rédaction dans la console.</summary>
public interface ILlmClient
{
    bool IsConfigured { get; }

    string Model { get; }

    Task<string> CompleteAsync(string system, string user, CancellationToken ct);
}

/// <summary>Section « Editorial » : compte officiel, rythme et fournisseur de rédaction.</summary>
public sealed class EditorialSettings
{
    public const string Section = "Editorial";

    /// <summary>Tâches automatiques (collecte, rédaction, publication). Faux par défaut, et toujours en revue Apple / Google.</summary>
    public bool Enabled { get; set; }

    public string OfficialName { get; set; } = "BéninConnect";
    public string OfficialEmail { get; set; } = "officiel@monbenin.com";

    public string OfficialBio { get; set; } =
        "Compte officiel de BéninConnect : actualités, culture et vie de la diaspora béninoise. Publications vérifiées par notre équipe.";

    public int CollectEveryMinutes { get; set; } = 60;
    public int DraftEveryMinutes { get; set; } = 15;
    public int PublishEveryMinutes { get; set; } = 5;
    public int MaxItemsPerSource { get; set; } = 10;

    /// <summary>Brouillons demandés au LLM par passage (coût maîtrisé).</summary>
    public int DraftBatchSize { get; set; } = 10;

    /// <summary>Publications au plus par passage (pas de rafale dans le fil).</summary>
    public int PublishBatchSize { get; set; } = 3;

    public LlmSettings Llm { get; set; } = new();
}

public sealed class LlmSettings
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-haiku-5-5";
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public int MaxTokens { get; set; } = 600;
}

/// <summary>Adresse d'article comparable : sans fragment ni paramètres de suivi (utm_*, fbclid…), paramètres triés.</summary>
public static partial class SourceUrl
{
    private static readonly string[] Tracking = ["fbclid", "gclid", "mc_cid", "mc_eid", "igshid", "ref", "ref_src"];

    public static string? Absolute(string? link, string feedUrl)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        // « /actu/1 » : relatif au flux (sous Linux, .NET le lirait sinon comme file:///actu/1)
        var value = link.Trim();
        Uri? uri = null;
        var absolute = value.Contains("://", StringComparison.Ordinal) && Uri.TryCreate(value, UriKind.Absolute, out uri);
        if (!absolute && !(Uri.TryCreate(feedUrl, UriKind.Absolute, out var root) && Uri.TryCreate(root, value, out uri)))
        {
            return null;
        }

        if (uri is null)
        {
            return null;
        }

        return uri.Scheme is "http" or "https" ? uri.ToString() : null;
    }

    public static string Normalize(string url)
    {
        var uri = new Uri(url);
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p =>
            {
                var key = p.Split('=')[0].ToLowerInvariant();
                return !key.StartsWith("utm_", StringComparison.Ordinal) && !Tracking.Contains(key);
            })
            .Order(StringComparer.Ordinal)
            .ToList();
        var path = uri.AbsolutePath.TrimEnd('/');
        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return query.Count > 0 ? $"{host}{path}?{string.Join('&', query)}" : $"{host}{path}";
    }

    public static string Hash(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(url))));

    /// <summary>Texte brut : balises retirées, entités décodées (&amp;amp;, &amp;#39;…), espaces réduits.</summary>
    public static string PlainText(string html) =>
        Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(html, " ")), " ").Trim();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
