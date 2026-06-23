#!/usr/bin/env bash
# Stop all GradePaper microservices, API Gateway, and local AIGradingService.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CORE_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
REPO_ROOT="$(cd "$CORE_ROOT/.." && pwd)"
COMPOSE_PATH="$REPO_ROOT/infra/docker/docker-compose.yml"
PID_DIR="$SCRIPT_DIR/.pids"

STOP_DOCKER=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --stop-docker) STOP_DOCKER=true; shift ;;
    -h|--help)
      echo "Usage: ./stop-all.sh [--stop-docker]"
      exit 0
      ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

PORTS=(5016 5055 5056 5057 5058 5059 5060 8080)

echo "=== GradePaper — stop all services ==="

stopped=0

if [[ -d "$PID_DIR" ]]; then
  for pid_file in "$PID_DIR"/*.pid; do
    [[ -f "$pid_file" ]] || continue
    pid="$(cat "$pid_file")"
    name="$(basename "$pid_file" .pid)"
    if kill -0 "$pid" 2>/dev/null; then
      echo "Stopping $name (PID $pid)..."
      kill "$pid" 2>/dev/null || true
      wait "$pid" 2>/dev/null || true
      stopped=$((stopped + 1))
    fi
    rm -f "$pid_file"
  done
fi

for port in "${PORTS[@]}"; do
  if command -v lsof >/dev/null 2>&1; then
    pids="$(lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
    for pid in $pids; do
      echo "Stopping PID $pid on port $port..."
      kill "$pid" 2>/dev/null || true
      stopped=$((stopped + 1))
    done
  fi
done

if [[ $stopped -eq 0 ]]; then
  echo "No running services found."
else
  echo "Stopped $stopped process(es)."
fi

if [[ "$STOP_DOCKER" == true ]]; then
  echo
  echo "Stopping Docker infrastructure..."
  docker compose -f "$COMPOSE_PATH" down
fi

echo "Done."
