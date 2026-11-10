# CI/CD GitHub Actions

| Workflow | Déclenchement | Rôle |
|---|---|---|
| `CI` (`.github/workflows/ci.yml`) | push sur `main`, pull request, manuel | tests de chaque partie ; sur `main`, images Docker publiées sur GHCR |
| `Deploy` (`.github/workflows/deploy.yml`) | après une CI verte sur `main`, ou manuel avec un tag | build de la console d'administration, puis Ansible `deploy.yml` : le serveur télécharge les images (pas de build sur le VPS), installe la configuration Caddy, le site public et la console |

## Jobs de la CI

| Job | Vérifie |
|---|---|
| Backend | build + tests d'intégration (PostgreSQL et MinIO via Testcontainers), architecture modulaire, migrations à jour |
| Console admin | oxlint, vitest, `tsc` + build Vite |
| Application mobile | `tsc --noEmit`, jest |
| Ansible | syntaxe des playbooks, ansible-lint |
| Site public et Caddy | `caddy validate` sur `server/Caddyfile`, liens internes du dossier `site/` |
| Images | `ghcr.io/<propriétaire>/beninconnect-api`, tags `sha-<commit>` et `latest` |

L'application Avalonia (Desktop, Android, iOS, `tests/`) n'est ni testée ni construite par la CI.

Une pull request doit passer tous les jobs (à rendre obligatoire : Settings > Branches > règle sur `main`).

## Site public (monbenin.com)

Le dossier `site/` (accueil, support, confidentialité, conditions, mentions légales, suppression de compte) est publié par le rôle Ansible `web` à chaque déploiement :
- `server/Caddyfile` copié dans `/etc/caddy/sites/beninconnect.caddy` (validé par `caddy validate` avant remplacement), Caddy rechargé s'il a changé ;
- `site/` synchronisé dans `/var/www/monbenin` (les fichiers retirés du dépôt sont supprimés du serveur).

Modifier une page : éditer le HTML dans `site/`, commit, push sur `main`. Prérequis : DNS `monbenin.com` et `www` vers le VPS.

## Console d'administration (admin.monbenin.com)

À chaque déploiement :
- le workflow `Deploy` construit `admin-console/` (`npm ci && npm run build`, `VITE_API_URL` vide) ;
- le rôle Ansible `web` synchronise `admin-console/dist` dans `/var/www/admin` ;
- Caddy sert la console sur `admin.monbenin.com` (retour sur `index.html` pour les routes React) et relaie `/api` et `/uploads` vers l'API : même origine, aucun réglage CORS.

Prérequis (une fois) : enregistrement DNS `A admin.monbenin.com → 51.255.40.214` chez OVH, puis un compte administrateur
(premier SuperAdmin créé au démarrage de l'API depuis `ADMIN_BOOTSTRAP_EMAIL` et `ADMIN_BOOTSTRAP_PASSWORD` du `.env`, voir `docs/MISE_EN_PROD.md`).
Depuis le Mac : `cd admin-console && npm ci && npm run build`, puis `ansible-playbook deploy.yml`.

## À configurer une fois sur GitHub

1. **Secrets** (Settings > Secrets and variables > Actions, ou dans l'environnement `production`) :

   | Secret | Contenu |
   |---|---|
   | `SSH_PRIVATE_KEY` | clé privée dédiée au déploiement (`ssh-keygen -t ed25519 -f deploy_github -N ""`) |
   | `SSH_KNOWN_HOSTS` | sortie de `ssh-keyscan -p <port> <ip du serveur>` |

2. Ajouter la clé publique `deploy_github.pub` à `deploy_authorized_keys` (`ansible/group_vars/production/main.yml`)
   puis relancer `ansible-playbook site.yml` depuis le Mac.
3. Déposer le `.env` sur le serveur avant le premier déploiement (voir `ansible/README.md`) : le workflow s'arrête s'il manque.
4. Recommandé : Settings > Environments > `production` > *Required reviewers* pour valider chaque mise en production.

## Versions et retour arrière

### Les versions

Chaque image de l'API porte une version, visible sur `https://api.monbenin.com/` (champ `version`)
et dans la console (page Système) :

| Événement | Tags de l'image |
|---|---|
| push sur `main` | `sha-1a2b3c4`, `latest` (déployée automatiquement) |
| tag Git `v1.2.0` | `1.2.0`, `1.2`, `sha-1a2b3c4` (pas de déploiement automatique) |

Nommer une version (après l'avoir testée en production, ou avant de la déployer) :

```bash
git tag v1.2.0 && git push origin v1.2.0
```

### Déployer une version

Actions > Deploy > Run workflow, champ **Version** : `1.2.0`, `sha-1a2b3c4` ou `latest`.
La console, le site, Caddy et le compose sont pris dans le **même commit** que l'image : tout revient ensemble.

### Revenir à une version précédente

Les images restent sur **GHCR** (jamais supprimées par la CI) ; le serveur les télécharge à chaque retour arrière,
même après un nettoyage de son cache. La version en production est `APP_VERSION` dans `server/.env.deploy`
(écrit par le déploiement, à côté du `.env` qui ne contient que les secrets et n'est jamais réécrit).

- **GitHub** (API, console, site et Caddy ensemble) : Actions > Deploy > Run workflow > `previous` ou une version.
- **Sur le serveur** (API seulement), menu des 5 dernières versions publiées sur GHCR
  (API GitHub Packages ; si GHCR est injoignable, celles de `deploy-history.log`) :

  ```
  $ cd /opt/beninconnect/server && ./rollback.sh      # en tant que ubuntu (deploy_user)
  Version en production : sha-c3d4e5f

    1) sha-c3d4e5f      publiée le 2026-11-12 à 09:38 UTC  ← en cours
    2) 1.1.0            publiée le 2026-11-10 à 17:55 UTC
    3) sha-9a8b7c6      publiée le 2026-11-08 à 21:02 UTC
    4) 1.0.0            publiée le 2026-11-02 à 20:10 UTC
    5) sha-1f2e3d4      publiée le 2026-11-01 à 12:30 UTC

  Version à redéployer (1-5, ou un tag, Entrée pour annuler) : 2
  ```

  Autres usages : `./rollback.sh --list`, `./rollback.sh 1.1.0` (confirmation), `./rollback.sh 1.1.0 -y` (sans confirmation).
  Si la version choisie ne répond pas sur `/health`, le script revient tout seul à la version précédente.

Connexion à GHCR de l'utilisateur de déploiement (`deploy_user`, `ubuntu` en production), une fois (inutile si le paquet est public) : jeton GitHub *classic* avec
le seul droit `read:packages`, puis

```bash
docker login ghcr.io -u <utilisateur GitHub>   # connecté en ubuntu ; coller le jeton comme mot de passe
```

Sans cette connexion, le script se rabat sur les images encore présentes sur le serveur (les 5 dernières, `keep_images`).
`previous` (GitHub) lit l'avant-dernière ligne de `deploy-history.log` : deux `previous` de suite reviennent à la version de départ.

### Base de données

Les migrations s'appliquent au démarrage de la nouvelle API ; **un retour arrière ne les annule pas**.
L'ancienne API fonctionne tant que les migrations ne font qu'ajouter (colonnes facultatives ou avec valeur par défaut, tables).
Avant une migration qui supprime ou renomme : sauvegarde manuelle (console > Sauvegardes), et migration en deux versions
(d'abord ne plus utiliser la colonne, puis la supprimer dans la version suivante).
