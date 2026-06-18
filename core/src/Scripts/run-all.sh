#!/usr/bin/env bash
# Start Docker infrastructure and run all GradePaper microservices + API Gateway.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CORE_ROOT="$(cd "$SRC_ROOT/.." && pwd)"
SOLUTION_PATH="$CORE_ROOT/GradingSystem.slnx"
COMPOSE_PATH="$SRC_ROOT/docker-compose.yml"
PID_DIR="$SCRIPT_DIR/.pids"

SKIP_DOCKER=false
SKIP_BUILD=false

usage() {
  cat <<'EOF'
Usage: ./run-all.sh [options]

Options:
  --skip-docker   Do not start docker-compose
  --skip-build    Skip dotnet build
  -h, --help      Show this help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-docker) SKIP_DOCKER=true; shift ;;
    --skip-build)  SKIP_BUILD=true; shift ;;
    -h|--help)     usage; exit 0 ;;
    *) echo "Unknown option: $1"; usage; exit 1 ;;
  esac
done

declare -a SERVICES=(
  "IamService|Services/IamService/IamService.API/IamService.API.csproj|5055"
  "ExamCatalogService|Services/ExamCatalogService/ExamCatalogService.API/ExamCatalogService.API.csproj|5056"
  "SubmissionService|Services/SubmissionService/SubmissionService.API/SubmissionService.API.csproj|5057"
  "GradingService|Services/GradingService/GradingService.API/GradingService.API.csproj|5058"
  "ReportingService|Services/ReportingService/ReportingService.API/ReportingService.API.csproj|5059"
  "NotificationService|Services/NotificationService/NotificationService.API/NotificationService.API.csproj|5060"
  "ApiGateway|ApiGateway/ApiGateway.csproj|5016"
)

port_in_use() {
  local port="$1"
  if command -v ss >/dev/null 2>&1; then
    ss -ltn | grep -q ":${port} "
  else
    lsof -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1
  fi
}

echo "=== GradePaper — run all services ==="
echo "Source root: $SRC_ROOT"
echo

if [[ "$SKIP_DOCKER" == false ]]; then
  echo "Starting infrastructure (docker compose)..."
  docker compose -f "$COMPOSE_PATH" up -d
  echo "Infrastructure is up."
  echo
fi

if [[ "$SKIP_BUILD" == false ]]; then
  echo "Building solution..."
  dotnet build "$SOLUTION_PATH"
  echo "Build succeeded."
  echo
fi

mkdir -p "$PID_DIR"

for entry in "${SERVICES[@]}"; do
  IFS='|' read -r name project port <<<"$entry"
  project_path="$SRC_ROOT/$project"

  if [[ ! -f "$project_path" ]]; then
    echo "Project not found: $project_path" >&2
    exit 1
  fi

  if port_in_use "$port"; then
    echo "[$name] Port $port already in use — skipping."
    continue
  fi

  log_file="$PID_DIR/${name}.log"
  echo "[$name] Starting on port $port (log: $log_file)..."

  (
    cd "$SRC_ROOT"
    export ASPNETCORE_ENVIRONMENT=Development
    dotnet run --project "$project_path" --launch-profile http
  ) >"$log_file" 2>&1 &

  echo $! >"$PID_DIR/${name}.pid"
  sleep 0.4
done

echo
echo "=== All services launched ==="
echo
echo "Endpoints:"
printf "  %-22s %s\n" "IamService"           "http://localhost:5055/swagger"
printf "  %-22s %s\n" "ExamCatalogService"   "http://localhost:5056/swagger"
printf "  %-22s %s\n" "SubmissionService"    "http://localhost:5057/swagger"
printf "  %-22s %s\n" "GradingService"       "http://localhost:5058/swagger"
printf "  %-22s %s\n" "ReportingService"     "http://localhost:5059/swagger"
printf "  %-22s %s\n" "NotificationService"  "http://localhost:5060/swagger"
printf "  %-22s %s\n" "ApiGateway"           "http://localhost:5016/swagger"
echo
echo "Logs: $PID_DIR/*.log"
echo "Stop all: ./stop-all.sh"
