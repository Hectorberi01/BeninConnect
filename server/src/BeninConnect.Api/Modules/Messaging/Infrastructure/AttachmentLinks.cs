using System.Security.Cryptography;
using System.Text;
using BeninConnect.Modules.Messaging.Application;
using Microsoft.AspNetCore.WebUtilities;

namespace BeninConnect.Modules.Messaging.Infrastructure;

/// <summary>Liens de pièces jointes signés HMAC, valables 10 minutes, pour un message et un membre.</summary>
public sealed class AttachmentLinks(IConfiguration config, TimeProvider clock) : IAttachmentLinks
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    // Clé dérivée de la clé JWT : un lien ne peut jamais servir de jeton
    private readonly byte[] key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"] ?? string.Empty), "attachment-links"u8);

    public AttachmentLinkDto Create(Guid messageId, Guid userId)
    {
        var expires = clock.GetUtcNow().Add(Lifetime);
        var exp = expires.ToUnixTimeSeconds();
        return new AttachmentLinkDto($"api/messages/{messageId}/attachment?u={userId}&exp={exp}&sig={Sign(messageId, userId, exp)}", expires);
    }

    public Guid? Verify(Guid messageId, Guid? userId, long? expires, string? signature)
    {
        if (userId is not { } user || expires is not { } exp || string.IsNullOrEmpty(signature) || exp < clock.GetUtcNow().ToUnixTimeSeconds())
        {
            return null;
        }

        var expected = Encoding.ASCII.GetBytes(Sign(messageId, user, exp));
        return CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(signature)) ? user : null;
    }

    private string Sign(Guid messageId, Guid userId, long exp) =>
        WebEncoders.Base64UrlEncode(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{messageId:N}:{userId:N}:{exp}")));
}
