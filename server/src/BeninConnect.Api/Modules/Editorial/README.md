# Module Editorial — compte officiel BéninConnect

Publications du compte officiel « BéninConnect » (badge vérifié) : actualités, culture et vie de la diaspora.
**Rien n'est publié sans la validation d'un administrateur** (console > Compte officiel).

## Flux

```
Sources RSS/Atom ─▶ Collected ─▶ (Claude) ─▶ Drafted ─▶ validation console ─▶ Approved ─▶ Published
                       │                         │                                  └─▶ Failed (3 essais) ─▶ Réessayer
                       └─▶ Skipped (hors sujet, sensible, rédaction impossible) ─▶ « Rédiger moi-même »
                    Rejected (par l'équipe, à toute étape)       Publication manuelle : Drafted directement
```

| Couche | Fichiers |
|---|---|
| Domain | `ContentCandidate` (états et transitions), `EditorialSource` |
| Application | `EditorialPipeline` (collecte, rédaction, publication), `AdminEditorial` (console), réglages et abstractions |
| Infrastructure | `EditorialDbContext` (schéma `editorial`), `XmlFeedReader` (RSS / Atom, sans DTD), `ClaudeLlmClient`, `EditorialWorker` |
| Api | `/api/admin/editorial/*`, permission `EditorialManage` (Super admin, Admin, Modérateur) |

Publication : contrat `IFeedModule.PublishAsAsync` (auteur = compte officiel, communauté sans adhésion, pas de notification de masse).
Compte officiel : `IIdentityModule.EnsureOfficialAccountAsync` (créé au premier besoin, connexion et messages refusés).

## Réglages (`Editorial__*`)

| Variable (.env) | Rôle | Défaut |
|---|---|---|
| `EDITORIAL_ENABLED` | Tâches automatiques (collecte 1 h, brouillons 15 min, publication 5 min) | `false` |
| `ANTHROPIC_API_KEY` | Rédaction par Claude ; vide : brouillons écrits dans la console | vide |
| `EDITORIAL_LLM_MODEL` | Modèle Claude | `claude-haiku-5-5` |
| `EDITORIAL_OFFICIAL_EMAIL` | Adresse du compte officiel (jamais utilisée pour se connecter) | `officiel@monbenin.com` |

L'environnement de revue Apple / Google n'exécute jamais ces tâches.
Sans `EDITORIAL_ENABLED`, tout reste utilisable depuis la console (boutons Collecter, Rédiger, publications manuelles) ;
seule la publication programmée attend alors « Publier maintenant ».

## Règles éditoriales

- Validation humaine obligatoire : l'administrateur qui valide est le responsable éditorial (journal d'audit).
  C'est aussi ce qui dispense de la mention « générée par IA » (AI Act, art. 50(4) : relecture et responsabilité éditoriale).
- Résumé reformulé + lien vers la source ; jamais l'article recopié (l'extrait sert seulement d'entrée au LLM).
- Images : uniquement des visuels dont BéninConnect détient les droits.
- Le compte est identifié comme compte officiel ; **aucun faux profil de membre**.
- Sources : privilégier les sources institutionnelles ou les médias qui autorisent la reprise (droit voisin des éditeurs de presse).
