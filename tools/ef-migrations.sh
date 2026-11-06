#!/usr/bin/env bash
# Ajoute une migration EF à chaque module (une base / un schéma par module).
#   tools/ef-migrations.sh InitialCreate          # tous les modules
#   tools/ef-migrations.sh AjoutX Feed            # un seul module
set -euo pipefail
cd "$(dirname "$0")/.."
NAME="${1:?Nom de la migration}"
MODULES="${2:-Identity Communities Feed Events Messaging Notifications Moderation Administration}"
PROJECT=server/src/BeninConnect.Api
dotnet tool restore >/dev/null
for m in $MODULES; do
  echo "== $m"
  dotnet tool run dotnet-ef migrations add "$NAME" -p "$PROJECT" -s "$PROJECT" \
    --context "${m}DbContext" -o "Modules/$m/Infrastructure/Persistence/Migrations"
done
