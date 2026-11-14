namespace BeninConnect.Modules.Voyages.Contracts;

/// <summary>Voyages (« Qui part au pays ? ») : vue des autres modules.</summary>
public interface IVoyagesModule
{
    /// <summary>Éléments à traiter par la modération (litiges, signalements, trajets masqués par signalements).</summary>
    Task<int> CountAwaitingReviewAsync(CancellationToken ct);
}
