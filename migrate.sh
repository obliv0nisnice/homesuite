echo "==> Prüfe PostgreSQL Verbindung"
if ! docker ps --format '{{.Names}}' | grep -qx 'homesuite-postgres'; then
  echo "Container homesuite-postgres läuft nicht"
  exit 1
fi

echo "==> Baue Backend (Release)"
cd "$SRC_ROOT"

dotnet build \
  "$API_PROJECT" \
  -c Release \
  --nologo

echo "==> Build erfolgreich"

echo "==> Führe EF Core Migrationen aus"
dotnet ef database update \
  --project "$INFRA_PROJECT" \
  --startup-project "$API_PROJECT"

echo "==> Fertig"
