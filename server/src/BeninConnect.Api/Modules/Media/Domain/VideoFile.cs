namespace BeninConnect.Modules.Media.Domain;

/// <summary>Vidéo d'une publication (compressée par l'application : 720p, 60 s) ; MP4 ou MOV reconnu sur le contenu.</summary>
public static class VideoFile
{
    public const long MaxBytes = 60 * 1024 * 1024;

    /// <summary>Extension d'après la signature ftyp (12 premiers octets).</summary>
    public static string? DetectExtension(ReadOnlySpan<byte> h)
    {
        if (h.Length < 12 || !h[4..8].SequenceEqual("ftyp"u8))
        {
            return null;
        }

        var brand = h[8..12];
        if (brand.SequenceEqual("qt  "u8))
        {
            return "mov";
        }

        ReadOnlySpan<string> mp4 = ["isom", "iso2", "iso4", "iso5", "iso6", "mp41", "mp42", "avc1", "M4V ", "3gp4", "3gp5", "3gp6", "MSNV", "dash"];
        foreach (var b in mp4)
        {
            if (brand.SequenceEqual(System.Text.Encoding.ASCII.GetBytes(b)))
            {
                return "mp4";
            }
        }

        return null;
    }
}
