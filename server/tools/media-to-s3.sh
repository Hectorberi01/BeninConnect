#!/usr/bin/env bash
# Copie les fichiers du volume Docker « media » (mode local) vers S3, avant de passer MEDIA_PROVIDER=s3.
# Sur le VPS, après avoir rempli S3_* dans .env :
#   ssh ubuntu@51.255.40.214 'cd /opt/beninconnect/server && bash -s' < server/tools/media-to-s3.sh
# Sans risque à relancer : seuls les fichiers absents du bucket sont copiés (rien n'est supprimé).
set -euo pipefail

get() { grep -E "^$1=" .env | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//'; }
ENDPOINT=$(get S3_SERVICE_URL); BUCKET=$(get S3_BUCKET); PRIVATE=$(get S3_PRIVATE_BUCKET)
: "${ENDPOINT:?S3_SERVICE_URL manquant dans .env}" "${BUCKET:?S3_BUCKET manquant dans .env}"

VOLUME=$(docker volume ls -q | grep -E '(^|_)media$' | head -1)
: "${VOLUME:?volume media introuvable}"
echo "Volume : $VOLUME -> $ENDPOINT"

aws() {
  docker run --rm -v "$VOLUME":/media:ro \
    -e AWS_ACCESS_KEY_ID="$(get S3_ACCESS_KEY)" -e AWS_SECRET_ACCESS_KEY="$(get S3_SECRET_KEY)" \
    -e AWS_DEFAULT_REGION="$(get S3_REGION || echo auto)" \
    amazon/aws-cli:2.22.0 --endpoint-url "$ENDPOINT" "$@"
}

# Photos et vidéos des publications (publiques)
aws s3 sync /media/uploads "s3://$BUCKET/uploads/" --only-show-errors
# Pièces jointes des discussions (privées)
aws s3 sync /media/private "s3://${PRIVATE:-$BUCKET}/private/" --only-show-errors
echo "Copie terminée. Passez MEDIA_PROVIDER=s3 dans .env puis : docker compose -f docker-compose.prod.yml --env-file .env --env-file .env.deploy up -d api"
