namespace BeninConnect.Modules.Services.Contracts;

/// <summary>Services entre membres : vue des autres modules (tableau de bord).</summary>
public interface IServicesModule
{
    /// <summary>Éléments en attente de modération (profils, offres masquées, signalements, justificatifs).</summary>
    Task<int> CountAwaitingReviewAsync(CancellationToken ct);
}

/// <summary>Clés des tâches de fond du module (planifiées par BackgroundJobs).</summary>
public static class ServicesTasks
{
    public const string ReviewReminders = "services.review-reminders";
}
