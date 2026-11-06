using System.Security.Cryptography;
using BeninConnect.Shared.Application;

namespace BeninConnect.Shared.Infrastructure;

/// <summary>PBKDF2-SHA256, 100 000 itérations, sel aléatoire. Format : v1.iterations.sel.hash (base64).</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string hash, string password)
    {
        var parts = hash.Split('.');
        if (parts is not ["v1", var iterationsText, var saltText, var keyText] || !int.TryParse(iterationsText, out var iterations))
        {
            return false;
        }

        var expected = Convert.FromBase64String(keyText);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(saltText), iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
