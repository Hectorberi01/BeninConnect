using System.Text.RegularExpressions;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Administration.Domain;

/// <summary>Bouton posé sur la bannière. Target : écran de l'application (BannerRules.Screens) ou lien https.</summary>
public sealed record BannerAction(string Label, string Icon, string Target);

/// <summary>
/// Bannière de l'accueil : une image créée hors de l'application (format 16:9), avec des boutons
/// facultatifs posés en bas et un lien facultatif sur toute l'image.
/// </summary>
public sealed class HomeBanner
{
    private HomeBanner()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Nom dans la console, lu aussi par les lecteurs d'écran.</summary>
    public string Title { get; private set; } = default!;

    public string ImagePath { get; private set; } = default!;

    /// <summary>Destination quand on touche l'image (hors boutons) ; null : aucune.</summary>
    public string? Target { get; private set; }

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

    /// <summary>Renvoie l'ancienne image si elle est remplacée (à libérer).</summary>
    public string? Update(BannerContent c, string by, DateTimeOffset now)
    {
        BannerRules.Validate(c);
        var previous = ImagePath is not null && ImagePath != c.ImagePath ? ImagePath : null;
        (Title, ImagePath, Target) = (c.Title.Trim(), c.ImagePath, string.IsNullOrWhiteSpace(c.Target) ? null : c.Target.Trim());
        Actions = c.Actions.Select(a => a with { Label = a.Label.Trim(), Target = a.Target.Trim() }).ToList();
        (IsActive, StartsAt, EndsAt, UpdatedBy, UpdatedAt) = (c.IsActive, c.StartsAt, c.EndsAt, by, now);
        return previous;
    }

    public void MoveTo(int position) => Position = position;
}

public sealed record BannerContent(
    string Title, string ImagePath, string? Target, IReadOnlyList<BannerAction> Actions, bool IsActive, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt);

public static partial class BannerRules
{
    public const int MaxTitle = 80;
    public const int MaxLabel = 24;
    public const int MaxActions = 4;
    public const int MaxUrl = 300;
    public const int MaxBanners = 20;

    /// <summary>Format de l'image : 16:9, 1600 × 900 px conseillé, 1080 × 608 px au minimum.</summary>
    public const int RecommendedWidth = 1600;
    public const int RecommendedHeight = 900;
    public const int MinWidth = 1080;
    public const double Ratio = 16d / 9d;
    public const double RatioTolerance = 0.03;

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

    /// <summary>Dimensions acceptées pour l'image d'une bannière.</summary>
    public static void ValidateImageSize(int width, int height)
    {
        if (width < MinWidth || height <= 0 || Math.Abs((double)width / height / Ratio - 1) > RatioTolerance)
        {
            throw new DomainException(
                $"Image de {width} × {height} px : il faut une image au format 16:9, {RecommendedWidth} × {RecommendedHeight} px conseillé ({MinWidth} px de large au minimum).");
        }
    }

    public static void Validate(BannerContent c)
    {
        var title = c.Title?.Trim() ?? "";
        if (title.Length is < 3 or > MaxTitle)
        {
            throw new DomainException($"Le nom doit faire entre 3 et {MaxTitle} caractères.");
        }

        if (string.IsNullOrEmpty(c.ImagePath) || !ImageRegex().IsMatch(c.ImagePath))
        {
            throw new DomainException("Ajoutez l'image de la bannière.");
        }

        if (!string.IsNullOrWhiteSpace(c.Target) && !IsValidTarget(c.Target.Trim()))
        {
            throw new DomainException("Lien de l'image invalide : écran de l'application ou lien https.");
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

/// <summary>Largeur et hauteur lues dans l'en-tête d'un JPEG, d'un PNG ou d'un WebP (sans décoder l'image).</summary>
public static class ImageSize
{
    public static (int Width, int Height)? Read(ReadOnlySpan<byte> data)
    {
        // PNG : bloc IHDR
        if (data.Length >= 24 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            return (BigEndian(data[16..20]), BigEndian(data[20..24]));
        }

        // WebP : VP8 (avec pertes), VP8L (sans pertes) ou VP8X (étendu)
        if (data.Length >= 30 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            var chunk = data[12..16];
            if (chunk.SequenceEqual("VP8 "u8))
            {
                return ((data[26] | (data[27] << 8)) & 0x3FFF, (data[28] | (data[29] << 8)) & 0x3FFF);
            }

            if (chunk.SequenceEqual("VP8L"u8))
            {
                var bits = data[21] | (data[22] << 8) | (data[23] << 16) | (data[24] << 24);
                return ((bits & 0x3FFF) + 1, ((bits >> 14) & 0x3FFF) + 1);
            }

            if (chunk.SequenceEqual("VP8X"u8))
            {
                return (1 + (data[24] | (data[25] << 8) | (data[26] << 16)), 1 + (data[27] | (data[28] << 8) | (data[29] << 16)));
            }

            return null;
        }

        // JPEG : premier segment SOF
        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < data.Length)
            {
                if (data[i] != 0xFF)
                {
                    i++;
                    continue;
                }

                var marker = data[i + 1];
                if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7 || marker == 0xFF)
                {
                    i += marker == 0xFF ? 1 : 2;
                    continue;
                }

                var length = (data[i + 2] << 8) | data[i + 3];
                if (marker is >= 0xC0 and <= 0xCF && marker is not 0xC4 and not 0xC8 and not 0xCC)
                {
                    return ((data[i + 7] << 8) | data[i + 8], (data[i + 5] << 8) | data[i + 6]);
                }

                i += 2 + length;
            }
        }

        return null;
    }

    private static int BigEndian(ReadOnlySpan<byte> b) => (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
}
