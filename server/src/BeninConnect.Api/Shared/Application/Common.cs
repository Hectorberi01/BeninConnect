namespace BeninConnect.Shared.Application;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Skip);

public sealed record CountDto(int Count);

public sealed record ReactionDto(Guid UserId, string Emoji);

public sealed record LabelCount(string Label, int Count);

/// <summary>Membre authentifié de la requête HTTP courante.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Identifiant du membre ; exception si la requête est anonyme.</summary>
    Guid Id { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hash, string password);
}

/// <summary>Volumétrie d'un module (page Système de la console).</summary>
public interface IModuleStatistics
{
    Task<IReadOnlyList<LabelCount>> CountTablesAsync(CancellationToken ct);
}

/// <summary>Réinitialisation du jeu de démonstration (développement uniquement, implémentée par l'hôte).</summary>
public interface IDemoDataReset
{
    Task ResetAsync(CancellationToken ct);
}

public static class Text
{
    public static string Excerpt(string? text, int max = 80)
    {
        var value = (text ?? string.Empty).ReplaceLineEndings(" ").Trim();
        return value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";
    }

    public static (int Skip, int Take) Page(int skip, int take) => (Math.Max(0, skip), Math.Clamp(take, 1, 100));

    public static string? Like(string? search) => string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();
}

public static class ReactionMapping
{
    public static List<ReactionDto> ToDto(this IEnumerable<Domain.Reaction> reactions) =>
        reactions.OrderBy(r => r.ReactedAt).Select(r => new ReactionDto(r.UserId, r.Emoji)).ToList();
}
