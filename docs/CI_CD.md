# CI/CD GitHub Actions

| Workflow | Déclenchement | Rôle |
|---|---|---|
| `CI` (`.github/workflows/ci.yml`) | push sur `main`, pull request, manuel | tests de chaque partie ; sur `main`, images Docker publiées sur GHCR |
| `Deploy` (`.github/workflows/deploy.yml`) | après une CI verte sur `main`, ou manuel avec un tag | Ansible `deploy.yml` : le serveur télécharge les images (pas de build sur le VPS) |

## Jobs de la CI

| Job | Vérifie |
|---|---|
| Backend | build + tests d'intégration (PostgreSQL et MinIO via Testcontainers), architecture modulaire, migrations à jour |
| Console admin | oxlint, vitest, `tsc` + build Vite |
| Application mobile | `tsc --noEmit`, jest |
| Ansible | syntaxe des playbooks, ansible-lint |
| Images | `ghcr.io/<propriétaire>/beninconnect-api` et `beninconnect-admin`, tags `sha-<commit>` et `latest` |

L'application Avalonia (Desktop, Android, iOS, `tests/`) n'est ni testée ni construite par la CI.

Une pull request doit passer tous les jobs (à rendre obligatoire : Settings > Branches > règle sur `main`).

## À configurer une fois sur GitHub

1. **Secrets** (Settings > Secrets and variables > Actions, ou dans l'environnement `production`) :

   | Secret | Contenu |
   |---|---|
   | `SSH_PRIVATE_KEY` | clé privée dédiée au déploiement (`ssh-keygen -t ed25519 -f deploy_github -N ""`) |
   | `SSH_KNOWN_HOSTS` | sortie de `ssh-keyscan -p <port> <ip du serveur>` |
   | `ANSIBLE_VAULT_PASSWORD` | contenu de `ansible/.vault_pass` |

2. Ajouter la clé publique `deploy_github.pub` à `deploy_authorized_keys` (`ansible/group_vars/production/main.yml`)
   puis relancer `ansible-playbook site.yml` depuis le Mac.
3. Committer `ansible/group_vars/production/vault.yml` **chiffré** (`ansible-vault encrypt`), jamais `.vault_pass`.
4. Recommandé : Settings > Environments > `production` > *Required reviewers* pour valider chaque mise en production.

## Déployer une version précise / revenir en arrière

Actions > Deploy > Run workflow > tag `sha-1a2b3c4` (liste des tags : onglet Packages du dépôt).
Les migrations de la base s'appliquent au démarrage de la nouvelle API ; un retour arrière ne les annule pas.
