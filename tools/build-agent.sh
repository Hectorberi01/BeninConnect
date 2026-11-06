#!/usr/bin/env bash
# Agent de build local : exécute UNIQUEMENT les commandes de la liste ci-dessous quand
# le fichier .agent/request contient leur nom. Arrêt : Ctrl+C.
# Utile quand un assistant prépare le code à distance mais que le SDK .NET est sur ce Mac.
set -u
cd "$(dirname "$0")/.."
ROOT="$(pwd)"
mkdir -p .agent
rm -f .agent/request
echo "Agent de build BéninConnect actif dans $ROOT (Ctrl+C pour arrêter)"

run() {
  case "$1" in
    info)     dotnet --info && dotnet workload list ;;
    restore)  dotnet restore src/BeninConnect.Desktop/BeninConnect.Desktop.csproj && dotnet restore tests/BeninConnect.Tests/BeninConnect.Tests.csproj && dotnet restore tests/BeninConnect.UiTests/BeninConnect.UiTests.csproj ;;
    build)    dotnet build src/BeninConnect.Desktop/BeninConnect.Desktop.csproj -nologo -clp:NoSummary ;;
    test)     dotnet test tests/BeninConnect.Tests/BeninConnect.Tests.csproj -nologo && dotnet test tests/BeninConnect.UiTests/BeninConnect.UiTests.csproj -nologo ;;
    unittest) dotnet test tests/BeninConnect.Tests/BeninConnect.Tests.csproj -nologo ;;
    uitest)   dotnet test tests/BeninConnect.UiTests/BeninConnect.UiTests.csproj -nologo --logger "console;verbosity=normal" ;;
    android)  dotnet build src/BeninConnect.Android/BeninConnect.Android.csproj -c Debug -nologo -clp:NoSummary ;;
    ios)      dotnet build src/BeninConnect.iOS/BeninConnect.iOS.csproj -c Debug -nologo -clp:NoSummary -p:RuntimeIdentifier=iossimulator-arm64 ;;
    run)      pkill -f BeninConnect.Desktop >/dev/null 2>&1; (dotnet run --project src/BeninConnect.Desktop/BeninConnect.Desktop.csproj > .agent/app.log 2>&1 &) ; sleep 8; echo "Application lancée" ;;
    serverbuild) dotnet build server/BeninConnect.Server.sln -nologo -clp:NoSummary ;;
    servertest)  dotnet test server/tests/BeninConnect.Api.Tests/BeninConnect.Api.Tests.csproj -nologo --logger "console;verbosity=normal" ;;
    dbup)        docker compose -f server/docker-compose.yml up -d db ;;
    apiup)       docker compose -f server/docker-compose.yml --profile full up -d --build && sleep 8 && curl -s -X POST http://localhost:5080/api/dev/reset && curl -s http://localhost:5080/health ;;
    apilogs)     docker compose -f server/docker-compose.yml --profile full logs --tail 60 api ;;
    efmigrate)   ./tools/ef-migrations.sh "${ARG:-InitialCreate}" ;;
    serverrun)   pkill -f BeninConnect.Api >/dev/null 2>&1; (dotnet run --project server/src/BeninConnect.Api > .agent/api.log 2>&1 &) ; sleep 15; tail -20 .agent/api.log; curl -s http://localhost:5080/health ;;
    serverstop)  pkill -f BeninConnect.Api && echo "API arrêtée" ;;
    stop)     pkill -f BeninConnect.Desktop && echo "Application arrêtée" ;;
    *)        echo "Commande refusée : $1" ; return 2 ;;
  esac
}

while true; do
  if [ -f .agent/request ]; then
    cmd="$(tr -cd 'a-z' < .agent/request)"
    rm -f .agent/request .agent/status
    echo "→ $cmd"
    run "$cmd" > .agent/output.log 2>&1
    code=$?
    echo "$cmd $code" > .agent/status
    echo "  terminé ($code)"
  fi
  sleep 1
done
