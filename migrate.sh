#!/usr/bin/env bash
set -Eeuo pipefail

SRC_ROOT="/home/jakob/homesuite"
API_DIR="$SRC_ROOT/backend/src/HomeSuite.API"
API_PROJECT="$API_DIR/HomeSuite.API.csproj"

export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5432;Database=homesuite;Username=postgres;Password=postgres"

echo "==> Prüfe Voraussetzungen"
command -v dotnet >/dev/null 2>&1 || { echo "dotnet nicht gefunden"; exit 1; }
command -v docker >/dev/null 2>&1 || { echo "docker nicht gefunden"; exit 1; }

echo "==> Prüfe, ob PostgreSQL erreichbar ist"
if ! docker ps --format '{{.Names}}' | grep -qx 'homesuite-postgres'; then
  echo "Container homesuite-postgres läuft nicht"
  exit 1
fi

echo "==> Führe EF Core Migrationen aus"
pushd "$API_DIR" >/dev/null
dotnet ef database update --project "$API_PROJECT" --startup-project "$API_PROJECT"
popd >/dev/null

echo "==> Fertig"
