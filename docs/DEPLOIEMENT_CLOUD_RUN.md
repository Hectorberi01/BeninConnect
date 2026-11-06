# Déploiement sur Google Cloud Run

Alternative à `server/docker-compose.prod.yml` : pas de serveur à gérer, HTTPS fourni, facturation à l'usage.

| Élément | Sur VPS (Docker) | Sur Google Cloud |
|---|---|---|
| HTTPS, domaines | Traefik + Let's Encrypt | Cloud Run (certificat géré) |
| API | conteneur `api` | service Cloud Run `beninconnect-api` |
| Console admin | conteneur `admin` | service Cloud Run `beninconnect-admin` |
| Base | hébergée ailleurs | Cloud SQL PostgreSQL 17 (ou Neon/Supabase : `DB_MODE=externe`) |
| Photos | volume Docker `media` | bucket Cloud Storage monté sur `/app/media` |
| Secrets | `.env` | Secret Manager (`bc-*`) |
| Migrations | au démarrage de l'API | tâche Cloud Run Job `beninconnect-migrate`, avant chaque déploiement |
| Sauvegardes | conteneur `backup` | Cloud SQL : quotidiennes + restauration à la minute près |

## 1. Prérequis

- Un projet Google Cloud avec facturation activée, et `gcloud` installé (`brew install --cask google-cloud-sdk`).
- `gcloud auth login` puis `gcloud config set project <PROJECT_ID>`.

## 2. Première fois

```bash
cp deploy/cloudrun/config.env.example deploy/cloudrun/config.env   # adapter projet, région, bucket, SMTP
./deploy/cloudrun/deploy.sh setup
```

`setup` active les APIs, crée le registre d'images, le bucket des photos, un compte de service aux droits limités
(secrets, Cloud SQL, bucket), l'instance Cloud SQL et sa base, puis les secrets :
clé JWT et mot de passe de la base générés automatiquement ; mot de passe SMTP et du premier SuperAdmin demandés.
Création de l'instance Cloud SQL : 5 à 10 minutes.

## 3. Chaque version

```bash
./deploy/cloudrun/deploy.sh release
```

1. **build** : Cloud Build construit les deux images (pas besoin de Docker sur le Mac).
2. **migrate** : la tâche lance l'image de l'API avec `--migrate`, applique les migrations et s'arrête.
   Si elle échoue, rien n'est déployé.
3. **deploy** : nouvelle révision de l'API et de la console ; Cloud Run bascule le trafic dès que le conteneur écoute sur le port 8080.

Retour arrière : `gcloud run services update-traffic beninconnect-api --to-revisions <révision>=100 --region europe-west1`.

## 4. Domaines

`europe-west1` (Belgique) accepte le mappage de domaine direct ; `europe-west9` (Paris) ne l'accepte pas
(il faudrait un load balancer, environ 18 €/mois).

```bash
gcloud beta run domain-mappings create --service beninconnect-api   --domain api.beninconnect.app   --region europe-west1
gcloud beta run domain-mappings create --service beninconnect-admin --domain admin.beninconnect.app --region europe-west1
```

Ajouter chez le registrar les enregistrements DNS affichés (certificat actif en 15 min à quelques heures).
Le domaine doit d'abord être vérifié : `gcloud domains verify beninconnect.app`.

Premier déploiement sans domaine : mettre les URL `*.run.app` affichées dans `API_URL` et `ADMIN_URL`
puis relancer `release` (la console intègre l'URL de l'API à la construction, et l'API autorise l'origine de la console).

## 5. Différences de comportement à connaître

- **Démarrage à froid** : avec `API_MIN_INSTANCES=0`, le premier appel après une période calme prend 2 à 4 s.
  Passer à 1 dès qu'il y a de vrais utilisateurs.
- **Limites de débit** : comptées par instance (jusqu'à 4). Pour une protection globale : Cloud Armor (nécessite un load balancer).
- **Restriction IP de la console** : pas d'équivalent à `ADMIN_ALLOWED_IPS` sans load balancer + Cloud Armor.
  Alternative gratuite : `--no-allow-unauthenticated` sur `beninconnect-admin` + accès via IAP.
- **Emails** : le port 25 est bloqué sur Google Cloud ; le port 587 (Brevo, Mailjet...) fonctionne.
- **Photos** : servies par l'API depuis le bucket monté. Pour beaucoup de trafic : Cloud CDN devant le bucket.
- **Journaux** : Cloud Logging (`gcloud run services logs read beninconnect-api --region europe-west1`).

## 6. Coût indicatif (petit trafic)

| Poste | Ordre de grandeur |
|---|---|
| Cloud Run API + console, 0 instance minimum | souvent dans l'offre gratuite |
| Cloud Run API, 1 instance minimum | environ 10 à 15 €/mois |
| Cloud SQL `db-f1-micro` + 10 Go | environ 10 €/mois |
| Cloud Storage, Secret Manager, Cloud Build | quelques centimes à 1 €/mois |

Vérifier avec le simulateur de prix Google Cloud et poser une alerte budgétaire (Facturation > Budgets).
