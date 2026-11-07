# Ansible : serveur de production BéninConnect

Installe un VPS neuf (Ubuntu 24.04 / Debian 12) et y déploie `server/docker-compose.prod.yml`
(Traefik + API + console admin + MinIO + sauvegardes). La base PostgreSQL reste hébergée ailleurs.

| Rôle | Contenu |
|---|---|
| `common` | mises à jour, paquets, fuseau horaire, mises à jour de sécurité automatiques, swap, utilisateur `deploy` (clé SSH, sudo) |
| `docker` | Docker CE + Compose depuis le dépôt officiel, journaux limités, nettoyage hebdomadaire des images |
| `app` | copie du backend et de la console (sans tests ni mobile), `.env` généré depuis le vault, build + démarrage, attente de `/health` |
| `security` | SSH par clé uniquement (sans root, port configurable), pare-feu UFW (SSH, 80, 443), fail2ban |

## Prérequis (sur le Mac)

```bash
brew install ansible
cd ansible
ansible-galaxy collection install -r requirements.yml -p collections
```

## Première installation

1. DNS : `API_DOMAIN` et `ADMIN_DOMAIN` (enregistrements A) vers l'IP du serveur.
2. `inventory/production.yml` : IP du serveur.
3. `group_vars/production/main.yml` : domaines, base, SMTP, **ta clé SSH publique** dans `deploy_authorized_keys`.
4. Secrets :
   ```bash
   cp group_vars/production/vault.yml.example group_vars/production/vault.yml   # remplir
   openssl rand -base64 32 > .vault_pass && chmod 600 .vault_pass             # à garder hors du dépôt
   ansible-vault encrypt group_vars/production/vault.yml
   ```
5. Premier passage avec le compte fourni par l'hébergeur :
   ```bash
   ansible-playbook site.yml -e ansible_user=root
   ```
6. Ensuite, toujours avec `deploy` (déjà dans l'inventaire). Si `ssh_port` a changé, mettre la même valeur
   dans `ansible_port` de l'inventaire.

## Mises à jour

```bash
ansible-playbook deploy.yml                 # nouvelle version (code, .env) : reconstruit seulement si quelque chose a changé
ansible-playbook site.yml                   # système + Docker + application + sécurité
ansible-playbook deploy.yml --check --diff  # voir ce qui changerait
ansible-vault edit group_vars/production/vault.yml
```

## Deux modes de déploiement

- **Depuis le Mac** (`ansible-playbook deploy.yml`) : le code est copié et les images construites sur le serveur
  (prévoir 4 Go de RAM).
- **Depuis GitHub Actions** (workflow `Deploy`, voir `docs/CI_CD.md`) : `app_image_tag=sha-xxxxxxx`, le serveur
  télécharge les images de GHCR, seuls `docker-compose.prod.yml` et `.env` sont copiés. 2 Go de RAM suffisent.
  À la main : `ansible-playbook deploy.yml -e app_image_tag=sha-1a2b3c4 -e registry_user=<github> -e registry_token=<PAT read:packages>`.

## À savoir

- Docker contourne UFW pour les ports publiés : seul Traefik publie (80/443), MinIO et l'API restent internes.
- Changer `ssh_port` : le pare-feu n'ouvre que le nouveau port. Garder la session ouverte et tester une
  nouvelle connexion avant de la fermer.
- Après la première connexion du SuperAdmin, vider `vault_admin_bootstrap_password` et relancer `deploy.yml`.
- Sauvegardes : base dans `/opt/beninconnect/server/backups/daily`, photos dans `.../backups/media`.
  Les copier hors du serveur (Storage Box, S3) reste à mettre en place.
