using System.Text.RegularExpressions;

namespace BeninConnect.Modules.Media.Domain;

public enum AttachmentType
{
    Image,
    Audio,
    File
}

/// <summary>Pièce jointe d'une discussion (photo, PDF, message vocal) : format reconnu sur le contenu, 20 Mo au plus.</summary>
public static partial class AttachmentFile
{
    public const long MaxBytes = 20 * 1024 * 1024;

    public static bool IsValidName(string name) => NameRegex().IsMatch(name);

    public static string ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".m4a" => "audio/mp4",
        ".mp3" => "audio/mpeg",
        ".aac" => "audio/aac",
        ".wav" => "audio/wav",
        ".ogg" => "audio/ogg",
        ".caf" => "audio/x-caf",
        var ext => ImageFile.ContentType("x" + ext),
    };

    /// <summary>Type et extension d'après la signature (12 premiers octets), jamais d'après le nom.</summary>
    public static (AttachmentType Type, string Extension)? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length < 12)
        {
            return null;
        }

        if (ImageFile.DetectExtension(h) is { } image)
        {
            return (AttachmentType.Image, image);
        }

        if (h[..5].SequenceEqual("%PDF-"u8))
        {
            return (AttachmentType.File, "pdf");
        }

        // Enregistrements iOS / Android (AAC dans un conteneur MP4)
        if (h[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = h[8..12];
            if (brand.SequenceEqual("M4A "u8) || brand.SequenceEqual("isom"u8) || brand.SequenceEqual("mp42"u8)
                || brand.SequenceEqual("3gp4"u8) || brand.SequenceEqual("3gp5"u8))
            {
                return (AttachmentType.Audio, "m4a");
            }

            return null;
        }

        if (h[..3].SequenceEqual("ID3"u8) || (h[0] == 0xFF && (h[1] & 0xE6) == 0xE2))
        {
            return (AttachmentType.Audio, "mp3");
        }

        if (h[0] == 0xFF && (h[1] & 0xF6) == 0xF0)
        {
            return (AttachmentType.Audio, "aac");
        }

        if (h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WAVE"u8))
        {
            return (AttachmentType.Audio, "wav");
        }

        if (h[..4].SequenceEqual("OggS"u8))
        {
            return (AttachmentType.Audio, "ogg");
        }

        return h[..4].SequenceEqual("caff"u8) ? (AttachmentType.Audio, "caf") : null;
    }

    [GeneratedRegex("^[a-f0-9]{32}\\.(jpg|png|webp|heic|pdf|m4a|mp3|aac|wav|ogg|caf)$")]
    private static partial Regex NameRegex();
}
