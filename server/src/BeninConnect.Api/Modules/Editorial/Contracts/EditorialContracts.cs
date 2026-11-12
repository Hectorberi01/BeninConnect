namespace BeninConnect.Modules.Editorial.Contracts;

/// <summary>Contrat public du module éditorial (compte officiel).</summary>
public interface IEditorialModule
{
    /// <summary>Contenus en attente de validation dans la console.</summary>
    Task<int> CountAwaitingReviewAsync(CancellationToken ct);
}
