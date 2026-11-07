namespace BeninConnect.Modules.Notifications.Domain;

/// <summary>Téléphone d'un membre qui reçoit les notifications push (jeton Expo).</summary>
public sealed class PushDevice
{
    public const int MaxTokenLength = 200;

    private PushDevice()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;

    /// <summary>ios, android.</summary>
    public string Platform { get; private set; } = string.Empty;

    /// <summary>Catégories coupées par le membre dans les réglages de l'app.</summary>
    public List<string> MutedCategories { get; private set; } = [];

    public DateTimeOffset RegisteredAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    public static bool IsValidToken(string? token) =>
        token is { Length: > 0 and <= MaxTokenLength }
        && (token.StartsWith("ExponentPushToken[", StringComparison.Ordinal) || token.StartsWith("ExpoPushToken[", StringComparison.Ordinal))
        && token.EndsWith(']');

    public static PushDevice Register(Guid id, Guid userId, string token, string platform, IEnumerable<NotificationCategory> muted, DateTimeOffset now)
    {
        var device = new PushDevice { Id = id, Token = token, RegisteredAt = now };
        device.Refresh(userId, platform, muted, now);
        return device;
    }

    /// <summary>Même téléphone : préférences à jour. Autre compte connecté sur ce téléphone : le jeton lui est réattribué.</summary>
    public void Refresh(Guid userId, string platform, IEnumerable<NotificationCategory> muted, DateTimeOffset now)
    {
        UserId = userId;
        Platform = platform.Trim().ToLowerInvariant();
        MutedCategories = muted.Distinct().Select(c => c.ToString()).ToList();
        LastSeenAt = now;
    }

    public bool Accepts(NotificationCategory category) => !MutedCategories.Contains(category.ToString());
}
