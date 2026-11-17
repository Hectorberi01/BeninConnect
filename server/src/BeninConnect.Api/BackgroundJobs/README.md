# Tâches planifiées (BackgroundJobs)

Toutes les tâches de fond de l'API sont déclarées ici et lancées par un planificateur unique.
La logique de chaque tâche reste dans son module : le module enregistre une `IScheduledTask`
(Shared.Application) sous une clé déclarée dans ses Contracts, et ce dossier décide de l'horaire.

```
BackgroundJobs/
  BackgroundJobsSetup.cs   enregistrement + routes de la console
  Scheduling/              planificateur, exécution, historique, réglages
  Jobs/                    un fichier par tâche (clé, nom, description, horaire)
```

Règle vérifiée par `ArchitectureTests` : ce dossier ne voit que les Contracts des modules
(ni Domain, ni Application, ni Infrastructure, ni EF Core).

## Catalogue

| Clé | Module | Horaire (UTC) | Rôle |
| --- | --- | --- | --- |
| `administration.daily-backup` | Administration | contrôle toutes les 10 min | pg_dump quotidien après `Backups:DailyAtUtc`, rotation, copie distante |
| `administration.backup-mirror` | Administration | `Backups:Offsite:IntervalMinutes` | copie des sauvegardes vers S3 / R2 (si configuré) |
| `administration.backup-verify` | Administration | lundi 04:10 | dernière sauvegarde : < 48 h, gzip lisible, dump complet, copie distante identique |
| `administration.audit-retention` | Administration | chaque jour 03:40 | journal d'audit : entrées > `Audit:RetentionMonths` (12) supprimées |
| `jobs.import` | Emploi | chaque minute | sources d'offres dont l'intervalle est écoulé (`Jobs:WorkerEnabled`) |
| `editorial.publish` / `collect` / `draft` | Éditorial | `Editorial:*EveryMinutes` | publication, collecte, rédaction (`Editorial:Enabled`) |
| `services.review-reminders` | Services | toutes les heures | rappels d'avis |
| `voyages.trip-lifecycle` | Voyages | toutes les heures | rappels de la veille, départs, voyageurs suspendus |
| `notifications.broadcasts` | Notifications | chaque minute | publications du compte officiel : tous les membres notifiés par vagues (`Notifications:Broadcast:WaveSize`), un push par jour au plus |
| `notifications.purge` | Notifications | chaque jour 03:20 | lues > 90 j, toutes > 180 j, appareils push inactifs > 180 j |
| `identity.accounts-cleanup` | Identité | chaque jour 03:30 | codes de réinitialisation expirés, comptes non vérifiés > 30 j supprimés |
| `media.orphans` | Médias | dimanche 04:30 | fichiers publics non référencés (> 7 j) : rapport, suppression si `Media:OrphanCleanup:Delete=true` |
| `app-versions.detect-outdated` | Versions de l'app | chaque jour 08:40 | membres non à jour : prépare une relance à valider (rien n'est envoyé) |
| `app-versions.send-campaigns` | Versions de l'app | toutes les 2 min | envoi par lots des relances validées (push + email) |
| `review.reset` | Revue | `Review:ResetDailyAtUtc` | environnement de revue seulement : remise à zéro des données |

Restent à part : l'envoi des notifications push (file d'attente, `PushDispatcher`) et le chiffrement
des messages existants (lancé depuis le tableau de bord).

## Réglages

- `BackgroundJobs__Enabled=false` : aucune exécution planifiée (tests) ; le lancement manuel reste possible.
- `BackgroundJobs__Disabled__0=media.orphans` : ne pas planifier une tâche.
- Une tâche désactivée par son module (ex. `Editorial__Enabled=false`) n'est ni planifiée ni lançable.
- Une heure quotidienne passée au démarrage n'est pas rattrapée (la sauvegarde quotidienne, elle, l'est).

## Console

Page « Tâches planifiées » (SuperAdmin et Developer, permission `BackgroundJobsManage`) :
horaire, prochaine exécution, 10 dernières exécutions, bouton « Lancer maintenant » (journalisé dans l'audit).
L'historique est en mémoire : il repart à zéro au redémarrage de l'API.

- `GET /api/admin/background-jobs`
- `POST /api/admin/background-jobs/{clé}/run` → 202 (exécution en arrière-plan), 409 si déjà en cours ou désactivée

## Ajouter une tâche

1. Dans le module : une classe `IScheduledTask` (Infrastructure), une clé dans `Contracts` (`XTasks`),
   `services.AddKeyedScoped<IScheduledTask, MaTache>(XTasks.MaCle)`.
2. Ici : un fichier `Jobs/MaTacheJob.cs` qui implémente `IBackgroundJob` (découvert automatiquement).
3. Une ligne dans le catalogue ci-dessus.
