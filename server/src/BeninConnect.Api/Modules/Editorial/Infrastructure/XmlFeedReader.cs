using System.Xml;
using System.Xml.Linq;
using BeninConnect.Modules.Editorial.Application;

namespace BeninConnect.Modules.Editorial.Infrastructure;

/// <summary>Flux RSS 2.0 ou Atom. Entités externes et DTD ignorées (pas de lecture de fichiers ni d'appels réseau cachés).</summary>
public sealed class XmlFeedReader(IHttpClientFactory http) : IFeedReader
{
    public const string ClientName = "editorial-feeds";

    private const string NotAFeed =
        "Cette adresse est une page web, pas un flux RSS ou Atom. Essayez l'adresse du flux (souvent /feed ou /rss).";
    private const long MaxBytes = 5 * 1024 * 1024;

    public async Task<IReadOnlyList<FeedItem>> ReadAsync(string url, int max, CancellationToken ct)
    {
        using var response = await http.CreateClient(ClientName).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            throw new InvalidOperationException("Flux trop volumineux.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true, DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = MaxBytes
        });
        XDocument doc;
        try
        {
            doc = await XDocument.LoadAsync(reader, LoadOptions.None, ct);
        }
        catch (XmlException)
        {
            throw new InvalidOperationException(NotAFeed);
        }

        return Parse(doc, url, max);
    }

    public static IReadOnlyList<FeedItem> Parse(XDocument doc, string feedUrl, int max)
    {
        var root = doc.Root ?? throw new InvalidOperationException("Flux vide.");
        if (root.Name.LocalName is not ("rss" or "feed" or "RDF"))
        {
            throw new InvalidOperationException(NotAFeed);
        }

        // RSS : <item> (avec ou sans espace de noms RSS 1.0) ; Atom : <entry>
        var entries = root.Descendants().Where(e => e.Name.LocalName is "item" or "entry");
        var items = new List<FeedItem>();
        foreach (var entry in entries)
        {
            var title = SourceUrl.PlainText(Child(entry, "title") ?? string.Empty);
            var link = SourceUrl.Absolute(Link(entry), feedUrl);
            if (link is null || title.Length == 0)
            {
                continue;
            }

            var summary = SourceUrl.PlainText(Child(entry, "description") ?? Child(entry, "summary") ?? Child(entry, "content") ?? string.Empty);
            items.Add(new FeedItem(title, link, summary));
            if (items.Count >= max)
            {
                break;
            }
        }

        return items;
    }

    private static string? Child(XElement entry, string name) =>
        entry.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

    private static string? Link(XElement entry)
    {
        var links = entry.Elements().Where(e => e.Name.LocalName == "link").ToList();
        // Atom : href (rel alternate ou absent) ; RSS : texte de l'élément
        var atom = links.FirstOrDefault(l => l.Attribute("href") is not null && (string?)l.Attribute("rel") is null or "alternate");
        return (string?)atom?.Attribute("href") ?? links.FirstOrDefault()?.Value ?? Child(entry, "guid");
    }
}
