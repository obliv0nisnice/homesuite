#!/usr/bin/env bash
set -Eeuo pipefail
set -x

SRC_ROOT="/home/jakob/homesuite/backend"
INFRA_PROJECT="$SRC_ROOT/src/HomeSuite.Infrastructure/HomeSuite.Infrastructure.csproj"
API_PROJECT="$SRC_ROOT/src/HomeSuite.API/HomeSuite.API.csproj"

export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5432;Database=homesuite;Username=postgres;Password=postgres"

echo "==> Umgebung setzen"
export DOTNET_ROOT="/nix/store/hlibrz46y7irr7s1ls85jdrbqpmyyhly-dotnet-sdk-8.0.418/share/dotnet"
export PATH="$PATH:$HOME/.dotnet/tools"

echo "DOTNET_ROOT=$DOTNET_ROOT"
echo "PATH=$PATH"

echo "==> Prüfe dotnet"
dotnet --version

echo "==> Prüfe dotnet-ef"
command -v dotnet-ef
dotnet ef --version

echo "==> Prüfe PostgreSQL Verbindung"
docker ps --format '{{.Names}}' | grep -qx 'homesuite-postgres'

echo "==> Baue Backend (Release)"
cd "$SRC_ROOT"
dotnet build "$API_PROJECT" -c Release --nologo

echo "==> Führe EF Core Migrationen aus"
dotnet ef database update \
  --project "$INFRA_PROJECT" \
  --startup-project "$API_PROJECT"

echo "==> Fertig"
