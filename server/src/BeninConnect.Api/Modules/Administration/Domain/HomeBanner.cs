using System.Text.RegularExpressions;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Administration.Domain;

/// <summary>Couleurs de la bannière (dégradés définis dans l'application).</summary>
public enum BannerTheme
{
    Green,
    Night,
    Ocean,
    Sunset
}

/// <summary>Illustration : photo à droite, texte à gauche. Background : photo en fond, assombrie.</summary>
public enum BannerLayout
{
    Illustration,
    Background
}

/// <summary>Bouton de la bannière. Target : écran de l'application (BannerRules.Screens) ou lien https.</summary>
public sealed record BannerAction(string Label, string Icon, string Target);

/// <summary>Bannière de l'accueil de l'application, gérée depuis la console.</summary>
public sealed class HomeBanner
{
    private HomeBanner()
    {
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = default!;

    /// <summary>Mot du titre mis en valeur (jaune).</summary>
    public string? Highlight { get; private set; }

    public string? Subtitle { get; private set; }
    public string? ImagePath { get; private set; }
    public BannerLayout Layout { get; private set; }
    public BannerTheme Theme { get; private set; }
    public List<BannerAction> Actions { get; private set; } = [];
    public bool IsActive { get; private set; }
    public int Position { get; private set; }
    public DateTimeOffset? StartsAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    public static HomeBanner Create(BannerContent content, int position, string by, DateTimeOffset now)
    {
        var banner = new HomeBanner { Id = Guid.NewGuid(), Position = position, CreatedAt = now };
        banner.Update(content, by, now);
        return banner;
    }

    /// <summary>Renvoie l'ancienne photo si elle est remplacée ou retirée (à libérer).</summary>
    public string? Update(BannerContent c, string by, DateTimeOffset now)
    {
        BannerRules.Validate(c);
        var previous = ImagePath != c.ImagePath ? ImagePath : null;
        (Title, Highlight, Subtitle, ImagePath, Layout, Theme) = (c.Title.Trim(), Clean(c.Highlight), Clean(c.Subtitle), c.ImagePath, c.Layout, c.Theme);
        Actions = c.Actions.Select(a => a with { Label = a.Label.Trim(), Target = a.Target.Trim() }).ToList();
        (IsActive, StartsAt, EndsAt, UpdatedBy, UpdatedAt) = (c.IsActive, c.StartsAt, c.EndsAt, by, now);
        return previous;
    }

    public void MoveTo(int position) => Position = position;

    public bool IsVisible(DateTimeOffset now) => IsActive && (StartsAt is null || StartsAt <= now) && (EndsAt is null || EndsAt > now);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record BannerContent(
    string Title, string? Highlight, string? Subtitle, string? ImagePath, BannerLayout Layout, BannerTheme Theme,
    IReadOnlyList<BannerAction> Actions, bool IsActive, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt);

public static partial class BannerRules
{
    public const int MaxTitle = 80;
    public const int MaxSubtitle = 140;
    public const int MaxLabel = 24;
    public const int MaxActions = 4;
    public const int MaxUrl = 300;
    public const int MaxBanners = 20;

    /// <summary>Écrans de l'application ouverts par un bouton (l'application ignore une cible qu'elle ne connaît pas).</summary>
    public static readonly string[] Screens =
    [
        "communities", "community.new", "jobs", "events", "event.new", "services", "services.offer",
        "voyages", "voyages.publish", "post.new", "search", "notifications",
    ];

    [GeneratedRegex(@"^[a-z0-9-]{1,40}$")]
    private static partial Regex IconRegex();

    [GeneratedRegex(@"^uploads/[a-f0-9]{32}\.(jpg|png|webp|heic)$")]
    private static partial Regex ImageRegex();

    public static bool IsValidTarget(string target) =>
        Screens.Contains(target)
        || (target.Length <= MaxUrl && Uri.TryCreate(target, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(url.Host));

    public static void Validate(BannerContent c)
    {
        var title = c.Title?.Trim() ?? "";
        if (title.Length is < 3 or > MaxTitle)
        {
            throw new DomainException($"Le titre doit faire entre 3 et {MaxTitle} caractères.");
        }

        if (!string.IsNullOrWhiteSpace(c.Highlight) && !title.Contains(c.Highlight.Trim(), StringComparison.Ordinal))
        {
            throw new DomainException("Le mot mis en valeur doit apparaître dans le titre.");
        }

        if ((c.Subtitle?.Trim().Length ?? 0) > MaxSubtitle)
        {
            throw new DomainException($"Le sous-titre fait {MaxSubtitle} caractères au plus.");
        }

        if (c.ImagePath is not null && !ImageRegex().IsMatch(c.ImagePath))
        {
            throw new DomainException("Image invalide.");
        }

        if (c.Layout == BannerLayout.Background && c.ImagePath is null)
        {
            throw new DomainException("Une photo est nécessaire pour la mise en page « photo en fond ».");
        }

        if (c.Actions.Count > MaxActions)
        {
            throw new DomainException($"{MaxActions} boutons au plus.");
        }

        foreach (var a in c.Actions)
        {
            if (string.IsNullOrWhiteSpace(a.Label) || a.Label.Trim().Length > MaxLabel)
            {
                throw new DomainException($"Chaque bouton a un libellé de {MaxLabel} caractères au plus.");
            }

            if (!IconRegex().IsMatch(a.Icon ?? ""))
            {
                throw new DomainException($"Icône invalide : {a.Icon}.");
            }

            if (!IsValidTarget(a.Target?.Trim() ?? ""))
            {
                throw new DomainException($"Destination invalide pour « {a.Label.Trim()} » : écran de l'application ou lien https.");
            }
        }

        if (c.StartsAt is { } start && c.EndsAt is { } end && end <= start)
        {
            throw new DomainException("La fin de diffusion doit être après le début.");
        }
    }
}
