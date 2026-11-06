namespace BeninConnect.Shared.Domain;

/// <summary>Réactions autorisées sur les commentaires et les messages (une par membre).</summary>
public static class Reactions
{
    public static readonly IReadOnlyList<string> Allowed = ["❤️", "👍", "😂", "😮", "😢", "🙏"];

    public static string Normalize(string? emoji)
    {
        var value = emoji?.Trim() ?? string.Empty;
        return Allowed.Contains(value) ? value : throw new DomainException("Cette réaction n'est pas disponible.");
    }
}

/// <summary>Réaction d'un membre ; la clé (élément, membre) garantit une seule réaction par personne.</summary>
public abstract class Reaction
{
    public Guid UserId { get; protected set; }
    public string Emoji { get; protected set; } = string.Empty;
    public DateTimeOffset ReactedAt { get; protected set; }

    internal void Change(string emoji, DateTimeOffset now)
    {
        Emoji = emoji;
        ReactedAt = now;
    }

    /// <summary>
    /// Même emoji : retire la réaction. Autre emoji : la remplace. Retourne true si une réaction est en place.
    /// </summary>
    internal static bool Toggle<T>(List<T> reactions, Guid userId, string? emoji, DateTimeOffset now, Func<string, T> create)
        where T : Reaction
    {
        var value = Reactions.Normalize(emoji);
        var existing = reactions.FirstOrDefault(r => r.UserId == userId);
        if (existing is null)
        {
            reactions.Add(create(value));
            return true;
        }

        if (existing.Emoji == value)
        {
            reactions.Remove(existing);
            return false;
        }

        existing.Change(value, now);
        return true;
    }
}
