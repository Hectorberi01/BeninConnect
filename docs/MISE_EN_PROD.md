# Mise en production de l'API BéninConnect

Cible : un VPS avec Docker pour l'API et les sauvegardes (`server/docker-compose.prod.yml`), Caddy installé
directement sur le VPS pour HTTPS (`server/Caddyfile`), et une base PostgreSQL hébergée chez un fournisseur.
La console admin n'est pas déployée pour l'instant.

## Caddy (HTTPS)

```bash
sudo apt install -y caddy
sudo cp /opt/beninconnect/server/Caddyfile /etc/caddy/Caddyfile   # ou copier le fichier du dépôt
sudo systemctl reload caddy
```

Caddy obtient le certificat Let's Encrypt de `api.monbenin.com` dès que le DNS pointe vers le VPS
et relaie vers l'API, qui n'écoute que sur `127.0.0.1:8080`.

## 0. Base de données hébergée

| Fournisseur | Intérêt | Point d'attention |
|---|---|---|
| Neon | offre gratuite, branches de test, sauvegardes PITR | mise en veille sur l'offre gratuite (1er appel lent) |
| Supabase | offre gratuite, console SQL | prendre la connexion « Direct » ou « Session pooler », pas « Transaction » |
| Scaleway / OVHcloud | hébergé en France (RGPD), sauvegardes incluses | payant dès le départ |
| Hetzner (si VPS chez eux) | pas de service managé : PostgreSQL sur un 2e VPS | sauvegardes et mises à jour à votre charge |

Choisir une région proche du VPS (Europe de l'Ouest) : chaque requête fait l'aller-retour.

1. Créer la base `beninconnect` et un utilisateur dédié à l'application (pas le compte propriétaire).
2. Si le fournisseur filtre par IP, autoriser l'IP du VPS (et la vôtre pour les migrations).
3. Renseigner `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`, `DB_SSLMODE` dans `.env`.
   Mot de passe contenant `;` ou `'` : le choisir sans, ou le régénérer.
4. Vérifier depuis le VPS : `docker run --rm postgres:17-alpine psql "postgresql://USER:MDP@HOTE:5432/beninconnect?sslmode=require" -c "select version()"`.

L'API refuse de démarrer si la connexion n'est pas chiffrée ou pointe vers `localhost`.
Elle réessaie automatiquement les coupures réseau passagères (5 fois).

## 0 bis. Photos, vidéos et pièces jointes

Deux zones de stockage :
- **public** (`uploads/`) : photos et vidéos des publications, profils, couvertures. Servi sous `https://api.monbenin.com/uploads/<nom>`.
- **privé** (`private/`) : pièces jointes des discussions (photos, PDF, messages vocaux). Jamais servi directement :
  `GET /api/messages/{id}/attachment` vérifie que le demandeur participe à la conversation.

Par défaut : **disque du serveur** (`MEDIA_PROVIDER=local`, volume Docker `media`, dossiers `uploads/` et `private/`).
Sauvegarde : le service `media-backup` archive le volume chaque nuit dans `server/backups/media` (7 jours).

### Passer à S3 (recommandé avec la vidéo)

| Fournisseur | Prix indicatif | Remarque |
|---|---|---|
| Cloudflare R2 | 10 Go gratuits, puis ~0,015 $/Go/mois, **trafic sortant gratuit** | conseillé pour la vidéo |
| Hetzner Object Storage | ~5 €/mois (1 To inclus) | |
| Scaleway / OVHcloud | à l'usage | hébergés en France |

1. Créer **deux buckets** : `beninconnect-media` (photos et vidéos) et `beninconnect-private` (pièces jointes, **toujours privé**).
2. Créer une clé d'accès limitée à ces deux buckets (lecture + écriture).
3. Facultatif mais conseillé pour la vidéo : domaine public sur `beninconnect-media` uniquement
   (R2 : Settings > Custom Domains, ex. `media.monbenin.com`) → `S3_PUBLIC_BASE_URL=https://media.monbenin.com`.
   Les fichiers ne passent alors plus par le VPS (CDN Cloudflare). Sans domaine public, l'API relaie les fichiers,
   avec lecture partielle (Range) pour la vidéo.
4. Dans le `.env` du serveur :
   ```
   S3_SERVICE_URL=https://<compte>.r2.cloudflarestorage.com
   S3_REGION=auto
   S3_BUCKET=beninconnect-media
   S3_PRIVATE_BUCKET=beninconnect-private
   S3_ACCESS_KEY=...
   S3_SECRET_KEY=...
   S3_PUBLIC_BASE_URL=https://media.monbenin.com
   ```
5. Copier les fichiers existants (le volume reste intact, retour arrière possible) :
   `ssh ubuntu@51.255.40.214 'cd /opt/beninconnect/server && bash -s' < server/tools/media-to-s3.sh`
6. `MEDIA_PROVIDER=s3` dans `.env`, puis redémarrer l'API (ou relancer le déploiement).
7. Vérifier une photo existante, une vidéo et une pièce jointe dans l'application.

En base, les chemins restent `uploads/<nom>` : aucune donnée à modifier. Les noms de fichiers sont uniques et jamais
modifiés : le versionnage du bucket suffit comme sauvegarde.

## 0 ter. Emails avec Resend

1. Créer un compte sur resend.com, puis Domains > Add domain : `monbenin.com`.
2. Ajouter chez le registrar les enregistrements DNS affichés (DKIM, SPF via sous-domaine `send`, MX de retour),
   attendre le statut *Verified*. Recommandé en plus : un enregistrement DMARC `_dmarc.monbenin.com`
   (`v=DMARC1; p=none; rua=mailto:...`).
3. API Keys > Create : droit *Sending access*, limité au domaine `monbenin.com` → `RESEND_API_KEY`
   (dans le `.env` du serveur).
4. `EMAIL_FROM=no-reply@monbenin.com`. Les refus de Resend apparaissent dans les journaux de l'API
   (`docker compose logs api | grep Resend`) et dans Resend > Emails / Logs.

Test en local sans domaine : `Email__Provider=resend`, `Email__ResendApiKey=re_...`,
`Email__From=onboarding@resend.dev` (envoi possible uniquement vers l'adresse du compte Resend).

## 1. Migrations

Chaque module a son schéma PostgreSQL et ses migrations (`Modules/<Module>/Infrastructure/Persistence/Migrations`).
Les migrations `InitialCreate` sont générées ; elles sont appliquées au démarrage de l'API (ou par `--migrate`).
À chaque changement de modèle :

```bash
./tools/ef-migrations.sh AjoutChamp Feed     # un module
./tools/ef-migrations.sh AjoutChamp          # tous les modules
```

Le test `Migrations_are_up_to_date_with_the_model` échoue si une migration manque.

## 2. Sur le serveur

```bash
git clone <dépôt> beninconnect && cd beninconnect/server
cp .env.example .env && nano .env          # secrets, domaines, SMTP
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
docker compose -f docker-compose.prod.yml logs -f api
```

DNS : un enregistrement A `api.monbenin.com` vers l'IP du serveur, ports 80 et 443 ouverts (Caddy).

## 3. Ce que la configuration de production apporte

| Sujet | Réglage |
|---|---|
| Démarrage refusé si dangereux | clé JWT de dev, code de vérification fixe, emails non configurés, données de démo activées |
| Secrets | uniquement en variables d'environnement (`.env`, jamais commité) |
| Schéma | migrations EF de chaque module appliquées au démarrage (schémas `identity`, `feed`, `communities`…) |
| Emails | Resend (API HTTP, `EMAIL_PROVIDER=resend`) ou SMTP : code de vérification, nouveau code, mot de passe oublié |
| HTTPS | Caddy sur le VPS (`server/Caddyfile`) : Let's Encrypt automatique, HSTS, API sur 127.0.0.1:8080 |
| Proxy | en-têtes `X-Forwarded-*` pris en compte (vraie IP du client) |
| Limites de débit | 10/min/IP sur connexion, inscription, codes (membres et admin) ; 60 envois de photos/h ; 600 requêtes/min/IP |
| CORS | aucune origine autorisée (l'app mobile n'en a pas besoin) ; ajouter `Cors__AllowedOrigins__0` pour la future console |
| En-têtes | `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy` |
| Santé | `/health` vérifie la base ; utilisé par Caddy et par Ansible |
| Journaux | JSON sur la sortie standard (`docker logs`, Loki, Datadog) |
| Conteneur | utilisateur non root ; photos dans un bucket S3 (ou le volume `media` en mode local) |
| Base | hébergée, connexion TLS obligatoire, réessais automatiques ; sauvegardes du fournisseur + copie quotidienne dans `server/backups` (7 j / 4 sem. / 6 mois) |
| Développement seulement | `/api/dev/reset`, `/openapi/v1.json`, réponse simulée de messagerie, code `123456` |

## 4. Après le déploiement

- Se connecter à la console avec `ADMIN_BOOTSTRAP_EMAIL`, changer le mot de passe, créer les autres comptes admin.
- Retirer ensuite `ADMIN_BOOTSTRAP_PASSWORD` du `.env` (il ne sert plus).
- Application mobile : mettre `https://API_DOMAIN` comme adresse par défaut et retirer `NSAllowsArbitraryLoads` de `app.json` avant publication sur les stores.
- Tester une restauration : de préférence via le fournisseur (point dans le temps), sinon `gunzip -c backups/daily/beninconnect-<date>.sql.gz | psql "postgresql://...?sslmode=require"` sur une base vide.

## 5. Restant à prévoir

- Supervision : alerte si `/health` échoue (UptimeRobot, Better Stack…).
- Photos : suppression des fichiers orphelins (photo envoyée puis jamais utilisée, ou remplacée).
- Paquets signalés vulnérables à la compilation (`Microsoft.OpenApi`, `System.Security.Cryptography.Pkcs`) : monter les versions.
- Mentions légales, politique de confidentialité et suppression de compte (RGPD, exigence Apple / Google).
