using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BeninConnect.Shared.Infrastructure;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "beninconnect-api";
    public string Audience { get; set; } = "beninconnect-app";

    /// <summary>Clé HMAC d'au moins 32 caractères (production : variable d'environnement Jwt__SigningKey).</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int LifetimeDays { get; set; } = 7;

    /// <summary>Audience des jetons de la console : un jeton membre n'y est jamais accepté (et inversement).</summary>
    public string AdminAudience { get; set; } = "beninconnect-admin";

    public int AdminLifetimeHours { get; set; } = 12;

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));

    public string CreateToken(string audience, DateTime now, TimeSpan lifetime, IEnumerable<Claim> claims) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(Key(), SecurityAlgorithms.HmacSha256)
        });
}
