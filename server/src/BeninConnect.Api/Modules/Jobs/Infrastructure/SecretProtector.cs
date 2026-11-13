using System.Security.Cryptography;
using System.Text;
using BeninConnect.Modules.Jobs.Application;
using BeninConnect.Shared.Infrastructure;

namespace BeninConnect.Modules.Jobs.Infrastructure;

/// <summary>AES-GCM, clé dérivée de Jwt__SigningKey. Clé JWT changée : secrets à ressaisir dans la console.</summary>
public sealed class SecretProtector(JwtOptions jwt) : ISecretProtector
{
    private const string Prefix = "v1:";
    private readonly byte[] key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwt.SigningKey), 32,
        info: Encoding.UTF8.GetBytes("beninconnect/jobs/source-secrets"));

    public string Protect(string secret)
    {
        var plain = Encoding.UTF8.GetBytes(secret);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string? Unprotect(string protectedSecret)
    {
        try
        {
            if (!protectedSecret.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return null;
            }

            var data = Convert.FromBase64String(protectedSecret[Prefix.Length..]);
            var nonce = data[..12];
            var tag = data[12..28];
            var cipher = data[28..];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}
