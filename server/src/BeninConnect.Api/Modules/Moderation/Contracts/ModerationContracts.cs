namespace BeninConnect.Modules.Moderation.Contracts;

/// <summary>Type de contenu signalé (contrat public : utilisé par Feed, Identity et l'API JSON).</summary>
public enum ReportTarget
{
    Post,
    Comment,
    User,
    Community
}

/// <summary>Signalement visant un membre (console). Reason et Status en texte.</summary>
public sealed record MemberReport(Guid Id, Guid ReporterId, string Reason, string Status, string Details, DateTimeOffset CreatedAt);

public interface IModerationModule
{
    /// <summary>Signalements visant directement le membre, les plus récents d'abord.</summary>
    Task<IReadOnlyList<MemberReport>> GetReportsAboutMemberAsync(Guid userId, int take, CancellationToken ct);

    /// <summary>Nombre de signalements envoyés par le membre.</summary>
    Task<int> CountReportsFiledByAsync(Guid userId, CancellationToken ct);

    /// <summary>Nombre de signalements (tous statuts) par cible.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountReportsAsync(ReportTarget target, IReadOnlyCollection<Guid> targetIds, CancellationToken ct);

    /// <summary>Cibles ayant au moins un signalement ouvert.</summary>
    Task<IReadOnlyList<Guid>> GetOpenTargetIdsAsync(ReportTarget target, CancellationToken ct);

    Task<int> CountOpenAsync(CancellationToken ct);
}
