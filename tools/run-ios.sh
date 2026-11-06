#!/usr/bin/env bash
# Compile, installe et lance BéninConnect sur l'iPhone branché, avec la console de l'app.
set -euo pipefail
cd "$(dirname "$0")/.."
UDID="${UDID:-00008101-000C78C02E88001E}"
DEVICE="${DEVICE:-3F6F81A7-7883-5B31-BA3A-8CE9B649761E}"
SIGN="${SIGN:-DB3E434E86B764B02D5CBFE910415089D970E25E}"
# Repart d'un build propre : des modules AOT obsolètes font planter le runtime au démarrage.
rm -rf src/BeninConnect.iOS/bin src/BeninConnect.iOS/obj src/BeninConnect.App/bin src/BeninConnect.App/obj
xcrun devicectl device uninstall app --device "$DEVICE" com.hbatech.beninconnect >/dev/null 2>&1 || true
dotnet build src/BeninConnect.iOS -c Debug -nologo -clp:NoSummary \
  -p:RuntimeIdentifier=ios-arm64 -p:_DeviceName="$UDID" -p:CodesignKey="$SIGN"
xcrun devicectl device install app --device "$DEVICE" src/BeninConnect.iOS/bin/Debug/net9.0-ios/ios-arm64/BeninConnect.iOS.app
xcrun devicectl device process launch --console --terminate-existing --device "$DEVICE" com.hbatech.beninconnect 2>&1 | tee ios-console.log
