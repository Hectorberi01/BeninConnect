namespace BeninConnect.Modules.Moderation.Contracts;

/// <summary>Type de contenu signalé (contrat public : utilisé par Feed, Identity et l'API JSON).</summary>
public enum ReportTarget
{
    Post,
    Comment,
    User,
    Community
}

public interface IModerationModule
{
    /// <summary>Nombre de signalements (tous statuts) par cible.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountReportsAsync(ReportTarget target, IReadOnlyCollection<Guid> targetIds, CancellationToken ct);

    /// <summary>Cibles ayant au moins un signalement ouvert.</summary>
    Task<IReadOnlyList<Guid>> GetOpenTargetIdsAsync(ReportTarget target, CancellationToken ct);

    Task<int> CountOpenAsync(CancellationToken ct);
}
