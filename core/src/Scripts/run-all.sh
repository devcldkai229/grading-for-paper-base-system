#!/usr/bin/env bash
# Start Docker infrastructure and run all GradePaper microservices + API Gateway + AI services.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CORE_ROOT="$(cd "$SRC_ROOT/.." && pwd)"
REPO_ROOT="$(cd "$CORE_ROOT/.." && pwd)"
SOLUTION_PATH="$CORE_ROOT/GradingSystem.slnx"
COMPOSE_PATH="$SRC_ROOT/docker-compose.yml"
AI_PARSE_ROOT="$SRC_ROOT/Services/AIParseQuestionService"
AI_GRADING_ROOT="$SRC_ROOT/Services/AIGradingService"
PID_DIR="$SCRIPT_DIR/.pids"

SKIP_DOCKER=false
SKIP_BUILD=false
SKIP_AI=false
MIGRATE=false

usage() {
  cat <<'EOF'
Usage: ./run-all.sh [options]

Options:
  --skip-docker   Do not start docker-compose
  --skip-build    Skip dotnet build
  --skip-ai       Do not start local Python AI services
  --migrate       Run EF migrations before starting services
  -h, --help      Show this help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-docker) SKIP_DOCKER=true; shift ;;
    --skip-build)  SKIP_BUILD=true; shift ;;
    --skip-ai)     SKIP_AI=true; shift ;;
    --migrate)     MIGRATE=true; shift ;;
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

load_repo_env() {
  local env_file="$REPO_ROOT/.env"
  [[ -f "$env_file" ]] || return 0
  set -a
  # shellcheck disable=SC1090
  source <(grep -E '^[^#]+=' "$env_file" | sed 's/\r$//')
  set +a
}

sync_internal_api_keys() {
  load_repo_env
  export INTERNAL_API_KEY="${INTERNAL_API_KEY:-duN6gR1GLWwprRnNH4tWxKLZLqM9zDYWQM2x0S0tYfC}"
  export InternalAuth__ApiKey="$INTERNAL_API_KEY"
  export AiGradingServiceUrl="${AiGradingServiceUrl:-http://localhost:8081}"
  # Phase 2 gRPC (h2c) endpoints consumed by GradingService (UseGrpcClients defaults true).
  export ExamCatalogGrpcUrl="${ExamCatalogGrpcUrl:-http://localhost:5066}"
  export SubmissionGrpcUrl="${SubmissionGrpcUrl:-http://localhost:5067}"
  # Opt-in trace export: set OTEL_EXPORTER_OTLP_ENDPOINT (e.g. http://localhost:4317) in .env.
  export OTEL_EXPORTER_OTLP_ENDPOINT="${OTEL_EXPORTER_OTLP_ENDPOINT:-}"
}

ensure_ai_venv() {
  local service_root="$1"
  local venv_python="$service_root/.venv/bin/python"
  if [[ ! -x "$venv_python" ]]; then
    if ! command -v python3 >/dev/null 2>&1; then
      echo "python3 not found. Install Python 3.11+ or use --skip-ai." >&2
      exit 1
    fi
    echo "[AI] Creating venv at $service_root/.venv ..."
    python3 -m venv "$service_root/.venv"
    "$venv_python" -m pip install -q --upgrade pip
    "$venv_python" -m pip install -q -r "$service_root/requirements.txt"
  fi
}

start_python_ai() {
  local name="$1"
  local service_root="$2"
  local port="$3"

  if port_in_use "$port"; then
    echo "[$name] Port $port already in use — skipping."
    return 0
  fi

  load_repo_env
  export INTERNAL_API_KEY="${INTERNAL_API_KEY:-duN6gR1GLWwprRnNH4tWxKLZLqM9zDYWQM2x0S0tYfC}"
  export InternalAuth__ApiKey="$INTERNAL_API_KEY"
  export OPENAI_MODEL="${OPENAI_MODEL:-gpt-4o-mini}"
  export RABBITMQ_HOST="${RABBITMQ_HOST:-localhost}"
  export RABBITMQ_PORT="${RABBITMQ_PORT:-5673}"
  export RABBITMQ_USERNAME="${RABBITMQ_USERNAME:-root}"
  export RABBITMQ_PASSWORD="${RABBITMQ_PASSWORD:-rootpassword}"
  export OTEL_EXPORTER_OTLP_ENDPOINT="${OTEL_EXPORTER_OTLP_ENDPOINT:-}"

  ensure_ai_venv "$service_root"
  mkdir -p "$PID_DIR"
  local log_file="$PID_DIR/${name}.log"
  echo "[$name] Starting on port $port (log: $log_file)..."

  (
    cd "$service_root"
    "$service_root/.venv/bin/uvicorn" app.main:app --host 0.0.0.0 --port "$port" --reload
  ) >"$log_file" 2>&1 &

  echo $! >"$PID_DIR/${name}.pid"
}

echo "=== GradePaper — run all services ==="
echo "Source root: $SRC_ROOT"
echo

sync_internal_api_keys

if [[ "$MIGRATE" == true ]]; then
  "$SCRIPT_DIR/apply-migrations.sh"
  echo
fi

if [[ "$SKIP_DOCKER" == false ]]; then
  echo "Starting infrastructure (docker compose)..."
  docker compose -f "$COMPOSE_PATH" up -d postgres mongodb redis rabbitmq gotenberg
  echo "Infrastructure is up."
  echo
fi

if [[ "$SKIP_BUILD" == false ]]; then
  echo "Building solution..."
  dotnet build "$SOLUTION_PATH"
  echo "Build succeeded."
  echo
fi

if [[ "$SKIP_AI" == false ]]; then
  start_python_ai "AIParseQuestionService" "$AI_PARSE_ROOT" 8080
  sleep 0.4
  start_python_ai "AIGradingService" "$AI_GRADING_ROOT" 8081
  sleep 0.4
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
    export INTERNAL_API_KEY
    export InternalAuth__ApiKey
    export AiGradingServiceUrl
    export ExamCatalogGrpcUrl
    export SubmissionGrpcUrl
    dotnet run --project "$project_path" --launch-profile http
  ) >"$log_file" 2>&1 &

  echo $! >"$PID_DIR/${name}.pid"
  sleep 0.4
done

echo
echo "=== All services launched ==="
echo
echo "Endpoints:"
if [[ "$SKIP_AI" == false ]]; then
  printf "  %-26s %s\n" "AIParseQuestionService" "http://localhost:8080/docs"
  printf "  %-26s %s\n" "AIGradingService" "http://localhost:8081/docs"
fi
printf "  %-26s %s\n" "IamService"           "http://localhost:5055/swagger"
printf "  %-26s %s\n" "ExamCatalogService"   "http://localhost:5056/swagger"
printf "  %-26s %s\n" "SubmissionService"    "http://localhost:5057/swagger"
printf "  %-26s %s\n" "GradingService"       "http://localhost:5058/swagger"
printf "  %-26s %s\n" "ReportingService"     "http://localhost:5059/swagger"
printf "  %-26s %s\n" "NotificationService"  "http://localhost:5060/swagger"
printf "  %-26s %s\n" "ApiGateway"           "http://localhost:5016/swagger"
echo
echo "Logs: $PID_DIR/*.log"
echo "Stop all: ./stop-all.sh"
echo "Apply migrations only: ./apply-migrations.sh"
