using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Editorial.Domain;

/// <summary>Flux RSS ou Atom suivi par le compte officiel (géré dans la console).</summary>
public sealed class EditorialSource
{
    private EditorialSource()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastFetchedAt { get; private set; }
    public string? LastError { get; private set; }
    public int CollectedCount { get; private set; }

    public static EditorialSource Create(string name, string url, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Name = ValidName(name),
        Url = ValidUrl(url),
        IsEnabled = true,
        CreatedAt = now
    };

    public void Update(string name, string url, bool enabled)
    {
        Name = ValidName(name);
        Url = ValidUrl(url);
        IsEnabled = enabled;
    }

    public void MarkFetched(int added, DateTimeOffset now)
    {
        LastFetchedAt = now;
        LastError = null;
        CollectedCount += added;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        LastFetchedAt = now;
        LastError = error.Length > 500 ? error[..500] : error;
    }

    private static string ValidName(string name)
    {
        var value = name.Trim();
        return value.Length is >= 2 and <= 100 ? value : throw new DomainException("Nom de la source : 2 à 100 caractères.");
    }

    private static string ValidUrl(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
            ? uri.ToString()
            : throw new DomainException("Adresse du flux invalide (http ou https).");
}
