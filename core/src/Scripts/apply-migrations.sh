#!/usr/bin/env bash
# Apply EF Core migrations for ExamCatalog and Grading databases.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CORE_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
REPO_ROOT="$(cd "$CORE_ROOT/.." && pwd)"

POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-12345}"

if [[ -f "$REPO_ROOT/.env" ]]; then
  while IFS='=' read -r key value; do
    [[ "$key" =~ ^#.*$ ]] && continue
    [[ -z "${key// }" ]] && continue
    key="$(echo "$key" | xargs)"
    value="$(echo "$value" | xargs | tr -d '"')"
    if [[ "$key" == "ConnectionStrings__DefaultConnection" ]] && [[ "$value" =~ Password=([^;]+) ]]; then
      POSTGRES_PASSWORD="${BASH_REMATCH[1]}"
    fi
  done < <(grep -E '^[^#]+=' "$REPO_ROOT/.env" || true)
fi

EXAM_CATALOG_CS="Host=localhost;Port=5433;Database=gradepaper_exam_catalog;Username=postgres;Password=${POSTGRES_PASSWORD}"
GRADING_CS="Host=localhost;Port=5433;Database=gradepaper_grading;Username=postgres;Password=${POSTGRES_PASSWORD}"

echo "=== GradePaper — apply database migrations ==="
echo "Repo root: $REPO_ROOT"
echo

cd "$REPO_ROOT"

echo "[ExamCatalog] Updating database..."
dotnet ef database update \
  --project "core/src/Services/ExamCatalogService/ExamCatalogService.Infrastructure/ExamCatalogService.Infrastructure.csproj" \
  --startup-project "core/src/Services/ExamCatalogService/ExamCatalogService.API/ExamCatalogService.API.csproj" \
  --connection "$EXAM_CATALOG_CS"
echo "[ExamCatalog] Done."
echo

echo "[Grading] Updating database..."
dotnet ef database update \
  --project "core/src/Services/GradingService/GradingService.Infrastructure/GradingService.Infrastructure.csproj" \
  --startup-project "core/src/Services/GradingService/GradingService.API/GradingService.API.csproj" \
  --connection "$GRADING_CS"
echo "[Grading] Done."
echo

echo "All migrations applied."
