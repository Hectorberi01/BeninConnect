namespace BeninConnect.Shared.Application;

/// <summary>
/// Tâche de fond d'un module. Le module l'enregistre sous une clé (services.AddKeyedScoped&lt;IScheduledTask, X&gt;(clé)) ;
/// la planification, l'historique et le lancement manuel sont gérés par BackgroundJobs.
/// </summary>
public interface IScheduledTask
{
    /// <summary>false : désactivée par la configuration du module (ni planifiée ni lançable).</summary>
    bool Enabled { get; }

    /// <summary>Renvoie un résumé lisible (« 3 rappels envoyés »). Une exception marque l'exécution en échec.</summary>
    Task<string> RunAsync(ScheduledTaskRun run, CancellationToken ct);
}

/// <summary>Manual : lancée depuis la console (une tâche peut alors ignorer sa condition d'horaire).</summary>
public sealed record ScheduledTaskRun(bool Manual);

/// <summary>Photos publiques (uploads/…) encore utilisées par un module : jamais effacées par le nettoyage des fichiers orphelins.</summary>
public interface IMediaReferences
{
    Task<IReadOnlyCollection<string>> ImagePathsAsync(CancellationToken ct);
}
