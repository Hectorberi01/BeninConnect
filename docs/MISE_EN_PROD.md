# Mise en production de l'API BéninConnect

Cible : un serveur Linux (VPS) avec Docker pour l'API et la console (`server/docker-compose.prod.yml` :
Traefik, API, console admin, sauvegarde) et une base PostgreSQL hébergée chez un fournisseur.

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

## 0 bis. Photos : stockage objet S3

Par défaut, `docker-compose.prod.yml` embarque **MinIO** (compatible S3) : rien à créer chez un fournisseur.
Renseigner `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` dans `.env` ; le bucket `beninconnect-media` est créé au premier envoi.
MinIO n'a aucun port public (l'API relaie les photos). Console : publier temporairement `127.0.0.1:9001:9001`
sur le service `minio`, puis `ssh -L 9001:localhost:9001 serveur` et ouvrir http://localhost:9001.
Sauvegarde : le service `media-backup` archive le volume chaque nuit dans `server/backups/media` (7 jours).

MinIO Community est archivé et `minio/minio` a été retiré de Docker Hub (septembre 2026) ; `quay.io/minio` refuse aussi.
Image utilisée : `cgr.dev/chainguard/minio:latest` (Chainguard, maintenue, seul le tag `latest` est gratuit),
modifiable avec `MINIO_IMAGE` (ex. le fork communautaire `pgsty/silo`). Pour la durée, prévoir un fournisseur ci-dessous
ou un équivalent auto-hébergé compatible S3 (Garage, SeaweedFS) : seules les variables `S3_*` changent.

Fournisseurs externes (renseigner `S3_*` dans `.env`, `S3_CREATE_BUCKET=false`) :

Les photos des membres vont dans un bucket compatible S3 (`MEDIA_PROVIDER=s3`) : rien sur le disque du VPS,
stockage redondant chez le fournisseur, plusieurs instances de l'API possibles.
En base, les chemins restent `uploads/<nom>` et les applications appellent toujours `https://API_DOMAIN/uploads/<nom>` :
l'API redirige vers `S3_PUBLIC_BASE_URL` si elle est renseignée, sinon relaie le fichier.

| Fournisseur | Prix indicatif | Remarque |
|---|---|---|
| Cloudflare R2 | 10 Go gratuits, puis ~0,015 $/Go/mois, **trafic sortant gratuit** | domaine public + CDN Cloudflare en 2 clics : conseillé |
| Hetzner Object Storage | ~5 €/mois (1 To inclus) | même datacenter que le VPS |
| Scaleway | à l'usage | hébergé en France |
| AWS S3 | à l'usage, trafic sortant payant | `eu-west-3` (Paris) |

1. Créer le bucket `beninconnect-media` (privé).
2. Créer une clé d'accès limitée à ce bucket (lecture + écriture) : `S3_ACCESS_KEY`, `S3_SECRET_KEY`.
3. Recommandé : brancher un domaine public en lecture seule (R2 : Settings > Custom Domains, ex. `media.beninconnect.app`)
   et le mettre dans `S3_PUBLIC_BASE_URL`. Les photos ne passent alors plus par le VPS et sont mises en cache par le CDN.
4. Renseigner `S3_SERVICE_URL`, `S3_REGION`, `S3_BUCKET` dans `.env`.

Reprise de photos déjà stockées sur le disque (passage de `local` à `s3`) :

```bash
rclone copy /var/lib/docker/volumes/beninconnect-prod_media/_data/uploads r2:beninconnect-media/uploads --progress
```

Les photos ne sont jamais modifiées (nom unique) : le versionnage du bucket ou une réplication vers un 2e bucket
suffit comme sauvegarde.

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

DNS : deux enregistrements A (`API_DOMAIN`, `ADMIN_DOMAIN`) vers l'IP du serveur, ports 80 et 443 ouverts.

## 3. Ce que la configuration de production apporte

| Sujet | Réglage |
|---|---|
| Démarrage refusé si dangereux | clé JWT de dev, code de vérification fixe, emails non configurés, données de démo activées |
| Secrets | uniquement en variables d'environnement (`.env`, jamais commité) |
| Schéma | migrations EF de chaque module appliquées au démarrage (schémas `identity`, `feed`, `communities`…) |
| Emails | SMTP : code de vérification, nouveau code, mot de passe oublié (+ `POST /api/auth/reset-password`) |
| HTTPS | Traefik + Let's Encrypt, redirection http → https, HSTS |
| Proxy | en-têtes `X-Forwarded-*` pris en compte (vraie IP du client) |
| Limites de débit | 10/min/IP sur connexion, inscription, codes (membres et admin) ; 60 envois de photos/h ; 600 requêtes/min/IP |
| CORS | liste explicite (`Cors__AllowedOrigins__0`) ; la console passe par son domaine, sans CORS |
| En-têtes | `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy` |
| Santé | `/health` vérifie la base ; utilisé par Traefik |
| Journaux | JSON sur la sortie standard (`docker logs`, Loki, Datadog) |
| Conteneur | utilisateur non root ; photos dans un bucket S3 (ou le volume `media` en mode local) |
| Base | hébergée, connexion TLS obligatoire, réessais automatiques ; sauvegardes du fournisseur + copie quotidienne dans `server/backups` (7 j / 4 sem. / 6 mois) |
| Console admin | Nginx non root ; accès restreignable par IP (`ADMIN_ALLOWED_IPS`) |
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
