namespace BeninConnect.Modules.Voyages.Contracts;

/// <summary>Voyages (« Qui part au pays ? ») : vue des autres modules.</summary>
public interface IVoyagesModule
{
    /// <summary>Éléments à traiter par la modération (litiges, signalements, trajets masqués par signalements).</summary>
    Task<int> CountAwaitingReviewAsync(CancellationToken ct);
}

/// <summary>Clés des tâches de fond du module (planifiées par BackgroundJobs).</summary>
public static class VoyagesTasks
{
    public const string TripLifecycle = "voyages.trip-lifecycle";
}
