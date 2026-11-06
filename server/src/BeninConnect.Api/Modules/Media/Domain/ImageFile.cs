using System.Text.RegularExpressions;

namespace BeninConnect.Modules.Media.Domain;

/// <summary>Photo envoyée par un membre : nom unique (jamais modifié), format reconnu sur le contenu.</summary>
public static partial class ImageFile
{
    public const long MaxBytes = 10 * 1024 * 1024;

    /// <summary>Chemin public stocké par les autres modules : « uploads/{nom} ».</summary>
    public const string PathPrefix = "uploads/";

    public static string NewName(string extension) => $"{Guid.NewGuid():N}.{extension}";

    public static bool IsValidName(string name) => NameRegex().IsMatch(name);

    public static string ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        _ => "application/octet-stream",
    };

    /// <summary>Extension d'après la signature (12 premiers octets), jamais d'après le nom ou l'en-tête HTTP.</summary>
    public static string? DetectExtension(ReadOnlySpan<byte> h)
    {
        if (h.Length < 12)
        {
            return null;
        }

        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
        {
            return "jpg";
        }

        if (h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "png";
        }

        if (h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8))
        {
            return "webp";
        }

        var brand = h[8..12];
        var isHeic = h[4..8].SequenceEqual("ftyp"u8)
                     && (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) || brand.SequenceEqual("mif1"u8));
        return isHeic ? "heic" : null;
    }

    [GeneratedRegex("^[a-f0-9]{32}\\.(jpg|png|webp|heic)$")]
    private static partial Regex NameRegex();
}
