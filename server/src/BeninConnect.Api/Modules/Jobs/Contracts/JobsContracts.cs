namespace BeninConnect.Modules.Jobs.Contracts;

/// <summary>Offres d'emploi importées : vue des autres modules (tableau de bord).</summary>
public interface IJobsModule
{
    /// <summary>Offres masquées en attente de revue (mots-clés d'alerte, signalements).</summary>
    Task<int> CountAwaitingReviewAsync(CancellationToken ct);
}
