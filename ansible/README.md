# Ansible : serveur de production BéninConnect

Installe un VPS neuf (Ubuntu 24.04 / Debian 12) et y déploie `server/docker-compose.prod.yml`
(API + sauvegardes, photos sur disque). HTTPS : Caddy installé à la main sur le VPS (`server/Caddyfile`).
La base PostgreSQL reste hébergée ailleurs.

| Rôle | Contenu |
|---|---|
| `common` | mises à jour, paquets, fuseau horaire, mises à jour de sécurité automatiques, swap, utilisateur `deploy` (clé SSH, sudo) |
| `docker` | Docker CE + Compose depuis le dépôt officiel, journaux limités, nettoyage hebdomadaire des images |
| `app` | copie du backend (sans tests ni mobile), vérifie le `.env` déposé à la main, build ou téléchargement de l'image, attente de `/health` |
| `security` | SSH par clé uniquement (sans root, port configurable), pare-feu UFW (SSH, 80, 443), fail2ban |

## Prérequis (sur le Mac)

```bash
brew install ansible
cd ansible
ansible-galaxy collection install -r requirements.yml -p collections
```

## Première installation

1. DNS : `api.monbenin.com` (enregistrement A) vers l'IP du serveur.
2. `inventory/production.yml` : IP du serveur.
3. `group_vars/production/main.yml` : **tes clés SSH publiques** dans `deploy_authorized_keys`.
4. Secrets : le `.env` est déposé **à la main** sur le serveur, jamais par Ansible ni par la CI :
   ```bash
   ssh ubuntu@51.255.40.214 'sudo mkdir -p /opt/beninconnect/server && sudo chown ubuntu: /opt/beninconnect -R'
   scp server/.env.example ubuntu@51.255.40.214:/opt/beninconnect/server/.env
   ssh ubuntu@51.255.40.214 'chmod 600 /opt/beninconnect/server/.env && nano /opt/beninconnect/server/.env'
   ```
   Le déploiement s'arrête avec un message clair si ce fichier manque. Le copier aussi dans un gestionnaire
   de mots de passe : c'est la seule copie des secrets.
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
```

## Deux modes de déploiement

- **Depuis le Mac** (`ansible-playbook deploy.yml`) : le code est copié et les images construites sur le serveur
  (prévoir 4 Go de RAM).
- **Depuis GitHub Actions** (workflow `Deploy`, voir `docs/CI_CD.md`) : `app_image_tag=sha-xxxxxxx`, le serveur
  télécharge les images de GHCR, seuls `docker-compose.prod.yml` et `.env` sont copiés. 2 Go de RAM suffisent.
  À la main : `ansible-playbook deploy.yml -e app_image_tag=sha-1a2b3c4 -e registry_user=<github> -e registry_token=<PAT read:packages>`.

## À savoir

- L'API n'écoute que sur `127.0.0.1:8080` : seul Caddy (ports 80/443 ouverts par UFW) la rend publique.
- Changer `ssh_port` : le pare-feu n'ouvre que le nouveau port. Garder la session ouverte et tester une
  nouvelle connexion avant de la fermer.
- Après la première connexion du SuperAdmin, retirer `ADMIN_BOOTSTRAP_PASSWORD` du `.env` du serveur.
- Sauvegardes : base dans `/opt/beninconnect/server/backups/daily`, photos dans `.../backups/media`.
  Les copier hors du serveur (Storage Box, S3) reste à mettre en place.
