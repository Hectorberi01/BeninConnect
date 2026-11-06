#!/usr/bin/env bash
# Déploiement BéninConnect sur Google Cloud Run.
#   ./deploy/cloudrun/deploy.sh setup     # une fois : APIs, registre, bucket, compte de service, secrets, Cloud SQL
#   ./deploy/cloudrun/deploy.sh release   # à chaque version : build, migrations, déploiement API + console
#   ./deploy/cloudrun/deploy.sh migrate   # migrations seules
set -euo pipefail
cd "$(dirname "$0")/../.."
source deploy/cloudrun/config.env

TAG="${TAG:-$(git rev-parse --short HEAD 2>/dev/null || date +%Y%m%d%H%M)}"
IMAGES="$REGION-docker.pkg.dev/$PROJECT_ID/$REPO"
SA="beninconnect-run@$PROJECT_ID.iam.gserviceaccount.com"
CONNECTION="$PROJECT_ID:$REGION:$CLOUDSQL_INSTANCE"
gc() { gcloud --project "$PROJECT_ID" "$@"; }

secret() { # secret <nom> <valeur> : crée ou ajoute une version
  if gc secrets describe "$1" >/dev/null 2>&1; then printf '%s' "$2" | gc secrets versions add "$1" --data-file=-
  else printf '%s' "$2" | gc secrets create "$1" --replication-policy=automatic --data-file=-; fi
}

ask() { local v; read -rsp "$1 : " v; echo >&2; printf '%s' "$v"; }

setup() {
  gc services enable run.googleapis.com artifactregistry.googleapis.com cloudbuild.googleapis.com \
    secretmanager.googleapis.com sqladmin.googleapis.com
  gc artifacts repositories describe "$REPO" --location "$REGION" >/dev/null 2>&1 ||
    gc artifacts repositories create "$REPO" --repository-format=docker --location "$REGION"
  gcloud storage buckets describe "gs://$BUCKET" >/dev/null 2>&1 ||
    gcloud storage buckets create "gs://$BUCKET" --project "$PROJECT_ID" --location "$REGION" --uniform-bucket-level-access

  gc iam service-accounts describe "$SA" >/dev/null 2>&1 ||
    gc iam service-accounts create beninconnect-run --display-name "BéninConnect Cloud Run"
  for role in roles/secretmanager.secretAccessor roles/cloudsql.client; do
    gc projects add-iam-policy-binding "$PROJECT_ID" --member "serviceAccount:$SA" --role "$role" --condition=None >/dev/null
  done
  gcloud storage buckets add-iam-policy-binding "gs://$BUCKET" --member "serviceAccount:$SA" --role roles/storage.objectUser >/dev/null

  if [ "$DB_MODE" = cloudsql ]; then
    if ! gc sql instances describe "$CLOUDSQL_INSTANCE" >/dev/null 2>&1; then
      gc sql instances create "$CLOUDSQL_INSTANCE" --database-version POSTGRES_17 --edition ENTERPRISE \
        --tier "$CLOUDSQL_TIER" --region "$REGION" --storage-auto-increase \
        --backup-start-time 02:00 --enable-point-in-time-recovery
    fi
    gc sql databases describe beninconnect --instance "$CLOUDSQL_INSTANCE" >/dev/null 2>&1 ||
      gc sql databases create beninconnect --instance "$CLOUDSQL_INSTANCE"
    local pwd; pwd="$(openssl rand -hex 24)"
    gc sql users create beninconnect_app --instance "$CLOUDSQL_INSTANCE" --password "$pwd" 2>/dev/null ||
      gc sql users set-password beninconnect_app --instance "$CLOUDSQL_INSTANCE" --password "$pwd"
    secret bc-db-connection "Host=/cloudsql/$CONNECTION;Database=beninconnect;Username=beninconnect_app;Password=$pwd;Maximum Pool Size=10"
  else
    secret bc-db-connection "$(ask 'Chaîne de connexion complète (avec SSL Mode=Require)')"
  fi

  secret bc-jwt-signing-key "$(openssl rand -base64 48)"
  secret bc-smtp-password "$(ask 'Mot de passe SMTP')"
  secret bc-admin-bootstrap-password "$(ask 'Mot de passe du premier SuperAdmin (10 caractères min.)')"
  echo "Setup terminé. Lancez : $0 release"
}

env_file() { # variables non secrètes, communes à l'API et à la tâche de migration
  local f; f="$(mktemp)"
  cat > "$f" <<YAML
ASPNETCORE_ENVIRONMENT: Production
Database__InitializeOnStartup: "false"
Email__Provider: smtp
Email__Host: "$SMTP_HOST"
Email__Port: "$SMTP_PORT"
Email__Username: "$SMTP_USERNAME"
Email__From: "$SMTP_FROM"
Admin__BootstrapEmail: "$ADMIN_BOOTSTRAP_EMAIL"
Cors__AllowedOrigins__0: "$ADMIN_URL"
Proxy__ForwardLimit: "1"
YAML
  echo "$f"
}

SECRETS="ConnectionStrings__Database=bc-db-connection:latest,Jwt__SigningKey=bc-jwt-signing-key:latest,Email__Password=bc-smtp-password:latest,Admin__BootstrapPassword=bc-admin-bootstrap-password:latest"
sql_flag() { [ "$DB_MODE" = cloudsql ] && echo "$1=$CONNECTION" || true; }

build() {
  gcloud builds submit --project "$PROJECT_ID" --region "$REGION" --config deploy/cloudrun/cloudbuild.yaml \
    --substitutions "_REGION=$REGION,_REPO=$REPO,_TAG=$TAG,_API_URL=$API_URL" .
}

migrate() {
  local env; env="$(env_file)"
  # shellcheck disable=SC2046
  gc run jobs deploy beninconnect-migrate --region "$REGION" --image "$IMAGES/api:$TAG" \
    --service-account "$SA" --args=--migrate --env-vars-file "$env" --set-secrets "$SECRETS" \
    --max-retries 0 --task-timeout 600 $(sql_flag --set-cloudsql-instances)
  gc run jobs execute beninconnect-migrate --region "$REGION" --wait
}

deploy() {
  local env; env="$(env_file)"
  # shellcheck disable=SC2046
  gc run deploy beninconnect-api --region "$REGION" --image "$IMAGES/api:$TAG" \
    --service-account "$SA" --allow-unauthenticated --port 8080 \
    --execution-environment gen2 --cpu 1 --memory 512Mi --cpu-boost \
    --concurrency 80 --timeout 60 --min-instances "$API_MIN_INSTANCES" --max-instances "$API_MAX_INSTANCES" \
    --env-vars-file "$env" --set-secrets "$SECRETS" $(sql_flag --add-cloudsql-instances) \
    --add-volume "name=media,type=cloud-storage,bucket=$BUCKET,mount-options=uid=1654;gid=1654" \
    --add-volume-mount volume=media,mount-path=/app/media

  gc run deploy beninconnect-admin --region "$REGION" --image "$IMAGES/admin:$TAG" \
    --allow-unauthenticated --port 8080 --cpu 1 --memory 256Mi --min-instances 0 --max-instances 2

  echo "API     : $(gc run services describe beninconnect-api --region "$REGION" --format 'value(status.url)')"
  echo "Console : $(gc run services describe beninconnect-admin --region "$REGION" --format 'value(status.url)')"
}

case "${1:-}" in
  setup) setup ;;
  build) build ;;
  migrate) migrate ;;
  deploy) deploy ;;
  release) build && migrate && deploy ;;
  *) echo "Usage : $0 setup|release|build|migrate|deploy" >&2; exit 1 ;;
esac
