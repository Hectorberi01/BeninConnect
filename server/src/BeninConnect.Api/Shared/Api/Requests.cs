namespace BeninConnect.Shared.Api;

public sealed record IdsRequest(IReadOnlyList<Guid> Ids);

public sealed record ContentRequest(string Content);

public sealed record ReactionRequest(string Emoji);

public sealed record ReasonRequest(string? Reason);

/// <summary>Politiques de limitation de débit (configurées par l'hôte).</summary>
public static class RateLimits
{
    public const string Auth = "auth";
    public const string Uploads = "uploads";
}
