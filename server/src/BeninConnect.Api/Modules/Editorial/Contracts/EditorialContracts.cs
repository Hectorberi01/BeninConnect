namespace BeninConnect.Modules.Editorial.Contracts;

/// <summary>Contrat public du module éditorial (compte officiel).</summary>
public interface IEditorialModule
{
    /// <summary>Contenus en attente de validation dans la console.</summary>
    Task<int> CountAwaitingReviewAsync(CancellationToken ct);
}

/// <summary>Clés des tâches de fond du module (planifiées par BackgroundJobs).</summary>
public static class EditorialTasks
{
    public const string Publish = "editorial.publish";
    public const string Collect = "editorial.collect";
    public const string Draft = "editorial.draft";
}
