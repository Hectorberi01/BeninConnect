using System.Security.Cryptography;
using System.Text;

namespace BeninConnect.Modules.Messaging.Infrastructure;

/// <summary>Section « Messaging » : clé de chiffrement des messages en base.</summary>
public sealed class MessageEncryptionOptions
{
    public const string Section = "Messaging";

    /// <summary>Clé AES-256 en base64 (32 octets) : Messaging__EncryptionKey, générée par « openssl rand -base64 32 ».</summary>
    public string? EncryptionKey { get; set; }

    /// <summary>Anciennes clés (base64, séparées par des virgules) : encore lues après une rotation.</summary>
    public string? PreviousEncryptionKeys { get; set; }
}

/// <summary>
/// Chiffrement AES-256-GCM du texte des messages et des noms de pièces jointes.
/// Format stocké : « enc1:&lt;id de clé&gt;:&lt;base64(nonce | tag | chiffré)&gt; ». Une valeur sans préfixe est un ancien message en clair.
/// </summary>
public sealed class MessageCipher
{
    public const string Prefix = "enc1:";
    public const string Unreadable = "🔒 Message illisible";

    private readonly byte[] currentKey;
    private readonly string currentId;
    private readonly Dictionary<string, byte[]> keys = new(StringComparer.Ordinal);

    public MessageCipher(byte[] key, IEnumerable<byte[]>? previous = null)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("La clé de chiffrement des messages doit faire 32 octets.", nameof(key));
        }

        currentKey = key;
        currentId = KeyId(key);
        keys[currentId] = key;
        foreach (var old in previous ?? [])
        {
            keys.TryAdd(KeyId(old), old);
        }
    }

    /// <summary>Utilisé par la conversion EF Core (modèle partagé entre les contextes) ; défini au démarrage.</summary>
    public static MessageCipher Current { get; set; } = null!;

    public static bool IsProtected(string? value) => value?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    public string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain) || IsProtected(plain))
        {
            return plain;
        }

        var data = Encoding.UTF8.GetBytes(plain);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cipher = new byte[data.Length];
        using var aes = new AesGcm(currentKey, tag.Length);
        aes.Encrypt(nonce, data, cipher, tag);
        return $"{Prefix}{currentId}:{Convert.ToBase64String([.. nonce, .. tag, .. cipher])}";
    }

    /// <summary>Texte en clair ; ancien message non chiffré rendu tel quel ; clé inconnue ou données altérées : « Message illisible ».</summary>
    public string Reveal(string stored)
    {
        if (!IsProtected(stored))
        {
            return stored;
        }

        try
        {
            var rest = stored[Prefix.Length..];
            var colon = rest.IndexOf(':');
            if (colon <= 0 || !keys.TryGetValue(rest[..colon], out var key))
            {
                return Unreadable;
            }

            var bytes = Convert.FromBase64String(rest[(colon + 1)..]);
            var nonce = bytes[..12];
            var tag = bytes[12..28];
            var cipher = bytes[28..];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            return Unreadable;
        }
    }

    /// <summary>
    /// Clé de la configuration. Sans clé : dérivée de la clé JWT en développement et en test seulement
    /// (la production refuse de démarrer, voir Host/Production.cs).
    /// </summary>
    public static MessageCipher FromConfiguration(MessageEncryptionOptions options, string jwtSigningKey, bool allowDerivedKey)
    {
        var previous = (options.PreviousEncryptionKeys ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Decode).ToList();
        if (!string.IsNullOrWhiteSpace(options.EncryptionKey))
        {
            return new MessageCipher(Decode(options.EncryptionKey), previous);
        }

        if (!allowDerivedKey)
        {
            throw new InvalidOperationException("Messaging__EncryptionKey est obligatoire (openssl rand -base64 32).");
        }

        var derived = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwtSigningKey), 32,
            info: Encoding.UTF8.GetBytes("beninconnect/messaging/dev-key"));
        return new MessageCipher(derived, previous);
    }

    /// <summary>Clé base64 valide de 32 octets.</summary>
    public static bool IsValidKey(string? value)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(value) && Convert.FromBase64String(value.Trim()).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Decode(string value) =>
        IsValidKey(value) ? Convert.FromBase64String(value.Trim()) : throw new InvalidOperationException("Clé de chiffrement des messages invalide : 32 octets en base64 attendus.");

    private static string KeyId(byte[] key) => Convert.ToHexStringLower(SHA256.HashData(key))[..8];
}
