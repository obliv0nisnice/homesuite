#!/usr/bin/env bash
set -Eeuo pipefail

APP_ROOT="/var/lib/homesuite"
BACKEND_ROOT="$APP_ROOT/backend"
FRONTEND_ROOT="$APP_ROOT/frontend"

SRC_ROOT="/home/jakob/homesuite"
BACKEND_SRC="$SRC_ROOT/backend/src/HomeSuite.API"
BACKEND_PROJECT="$BACKEND_SRC/HomeSuite.API.csproj"
FRONTEND_SRC="$SRC_ROOT/frontend"

TIMESTAMP="$(date +%F_%H-%M-%S)"
TMP_ROOT="/tmp/homesuite-deploy-$TIMESTAMP"
BACKEND_TMP="$TMP_ROOT/backend"
FRONTEND_TMP="$TMP_ROOT/frontend"

BACKEND_RELEASE="$BACKEND_ROOT/releases/$TIMESTAMP"
FRONTEND_RELEASE="$FRONTEND_ROOT/releases/$TIMESTAMP"

APP_USER="homesuite"
APP_GROUP="homesuite"
BACKEND_DLL="HomeSuite.API.dll"

echo "==> Voraussetzungen prüfen"
command -v dotnet >/dev/null 2>&1 || { echo "dotnet nicht gefunden"; exit 1; }
command -v npm >/dev/null 2>&1 || { echo "npm nicht gefunden"; exit 1; }
command -v rsync >/dev/null 2>&1 || { echo "rsync nicht gefunden"; exit 1; }
command -v sudo >/dev/null 2>&1 || { echo "sudo nicht gefunden"; exit 1; }

echo "==> Temporäre Build-Ordner anlegen"
rm -rf "$TMP_ROOT"
mkdir -p "$BACKEND_TMP" "$FRONTEND_TMP"

echo "==> Backend bauen"
pushd "$BACKEND_SRC" >/dev/null
dotnet publish "$BACKEND_PROJECT" -c Release -o "$BACKEND_TMP"
popd >/dev/null

if [ ! -f "$BACKEND_TMP/$BACKEND_DLL" ]; then
  echo "Fehler: $BACKEND_DLL wurde nicht erzeugt"
  ls -lah "$BACKEND_TMP" || true
  exit 1
fi

echo "==> Frontend bauen"
pushd "$FRONTEND_SRC" >/dev/null
npm ci
npx vite build
rsync -av dist/ "$FRONTEND_TMP/"
popd >/dev/null

if [ ! -f "$FRONTEND_TMP/index.html" ]; then
  echo "Fehler: Frontend-Build unvollständig, index.html fehlt"
  ls -lah "$FRONTEND_TMP" || true
  exit 1
fi

echo "==> Release-Ordner anlegen"
sudo mkdir -p "$BACKEND_RELEASE" "$FRONTEND_RELEASE"

echo "==> Dateien ins Release kopieren"
sudo rsync -av --delete "$BACKEND_TMP/" "$BACKEND_RELEASE/"
sudo rsync -av --delete "$FRONTEND_TMP/" "$FRONTEND_RELEASE/"

echo "==> Symlinks umstellen"
sudo ln -sfn "$BACKEND_RELEASE" "$BACKEND_ROOT/current"
sudo ln -sfn "$FRONTEND_RELEASE" "$FRONTEND_ROOT/current"

echo "==> Ownership setzen"
sudo chown -R "$APP_USER:$APP_GROUP" "$APP_ROOT"

echo "==> Rechte für Backend setzen"
sudo chmod 755 "$APP_ROOT"
sudo chmod 755 "$BACKEND_ROOT" "$FRONTEND_ROOT"
sudo chmod 755 "$BACKEND_ROOT/releases" "$FRONTEND_ROOT/releases"
sudo find "$BACKEND_ROOT" -type d -exec chmod 755 {} \;
sudo find "$BACKEND_ROOT" -type f -exec chmod 644 {} \;

echo "==> Rechte für Frontend setzen"
sudo find "$FRONTEND_ROOT" -type d -exec chmod 755 {} \;
sudo find "$FRONTEND_ROOT" -type f -exec chmod 644 {} \;

echo "==> Backend neu starten"
sudo systemctl restart homesuite-backend

echo "==> Nginx neu laden"
sudo systemctl reload nginx

echo "==> Status Backend"
sudo systemctl --no-pager --full status homesuite-backend || true

echo "==> Status Nginx"
sudo systemctl --no-pager --full status nginx || true

echo "==> Fertig"
echo "Backend Release:  $BACKEND_RELEASE"
echo "Frontend Release: $FRONTEND_RELEASE"
echo
echo "Prüfen mit:"
echo "  curl -H \"Host: homesuite.nestlerlabs.at\" https://127.0.0.1 -k | head"
echo "  journalctl -u homesuite-backend -n 50 --no-pager"
echo "  journalctl -u nginx -n 50 --no-pager"
