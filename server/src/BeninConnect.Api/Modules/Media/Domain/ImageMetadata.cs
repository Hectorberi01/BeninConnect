using System.Buffers.Binary;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Modules.Media.Domain;

/// <summary>Retire les métadonnées (EXIF dont la localisation GPS, XMP, commentaires) sans réencoder l'image.</summary>
public static class ImageMetadata
{
    public static byte[] Strip(byte[] data, string extension) => extension switch
    {
        "jpg" => Jpeg(data),
        "png" => Png(data),
        "webp" => WebP(data),
        _ => throw new DomainException("Format non pris en charge : utilisez une photo JPEG, PNG ou WebP.")
    };

    // Segments APPn (EXIF, XMP, IPTC…) et COM retirés ; APP0 (JFIF), APP2 (profil couleur) et APP14 (Adobe) gardés
    private static byte[] Jpeg(byte[] d)
    {
        using var output = new MemoryStream(d.Length);
        output.Write(d, 0, 2);
        var i = 2;
        while (i + 4 <= d.Length)
        {
            if (d[i] != 0xFF)
            {
                throw Invalid();
            }

            var marker = d[i + 1];
            if (marker == 0xDA)
            {
                output.Write(d, i, d.Length - i); // début de l'image : le reste est copié tel quel
                return output.ToArray();
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(i + 2));
            if (length < 2 || i + 2 + length > d.Length)
            {
                throw Invalid();
            }

            var drop = (marker is >= 0xE1 and <= 0xEF && marker is not (0xE2 or 0xEE)) || marker == 0xFE;
            if (!drop)
            {
                output.Write(d, i, 2 + length);
            }

            i += 2 + length;
        }

        throw Invalid();
    }

    // Blocs texte et EXIF retirés ; les blocs d'image et de couleur sont gardés
    private static byte[] Png(byte[] d)
    {
        string[] dropped = ["eXIf", "tEXt", "zTXt", "iTXt", "tIME"];
        using var output = new MemoryStream(d.Length);
        output.Write(d, 0, 8);
        var i = 8;
        while (i + 12 <= d.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(i));
            if (length < 0 || i + 12 + length > d.Length)
            {
                throw Invalid();
            }

            var type = System.Text.Encoding.ASCII.GetString(d, i + 4, 4);
            if (!dropped.Contains(type))
            {
                output.Write(d, i, 12 + length);
            }

            i += 12 + length;
            if (type == "IEND")
            {
                return output.ToArray();
            }
        }

        throw Invalid();
    }

    // Blocs EXIF et XMP retirés, indicateurs de l'en-tête VP8X mis à jour
    private static byte[] WebP(byte[] d)
    {
        using var output = new MemoryStream(d.Length);
        output.Write(d, 0, 12);
        var i = 12;
        var vp8x = -1;
        while (i + 8 <= d.Length)
        {
            var type = System.Text.Encoding.ASCII.GetString(d, i, 4);
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(i + 4));
            var total = 8 + size + (size & 1);
            if (size < 0 || i + 8 + size > d.Length)
            {
                throw Invalid();
            }

            if (type is not ("EXIF" or "XMP "))
            {
                if (type == "VP8X")
                {
                    vp8x = (int)output.Position;
                }

                output.Write(d, i, Math.Min(total, d.Length - i));
            }

            i += total;
        }

        var bytes = output.ToArray();
        if (vp8x >= 0 && vp8x + 8 < bytes.Length)
        {
            bytes[vp8x + 8] &= unchecked((byte)~0x0C); // indicateurs EXIF (0x08) et XMP (0x04)
        }

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)(bytes.Length - 8));
        return bytes;
    }

    private static DomainException Invalid() => new("Image illisible ou endommagée.");
}
