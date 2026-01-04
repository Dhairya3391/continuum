#!/bin/zsh
# Run a single microservice with environment variables loaded from .env
# Usage: ./scripts/run-service.sh <service> [dotnet run args]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

# Load environment variables (JWT/DB/Rabbit/Redis/etc.)
source "$PROJECT_ROOT/scripts/load-env.sh"

typeset -A SERVICES
SERVICES=(
  identity "src/Services/Identity/PersonalUniverse.Identity.API"
  storage "src/Services/Storage/PersonalUniverse.Storage.API"
  personality "src/Services/PersonalityProcessing/PersonalUniverse.PersonalityProcessing.API"
  simulation "src/Services/SimulationEngine/PersonalUniverse.SimulationEngine.API"
  events "src/Services/EventService/PersonalUniverse.EventService.API"
  visualization "src/Services/VisualizationFeed/PersonalUniverse.VisualizationFeed.API"
)

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <identity|storage|personality|simulation|events|visualization> [dotnet run args]" >&2
  exit 1
fi

SERVICE_KEY="$1"
shift
SERVICE_PATH="${SERVICES[$SERVICE_KEY]:-}"

if [[ -z "$SERVICE_PATH" ]]; then
  echo "Unknown service '$SERVICE_KEY'. Valid options: ${(@k)SERVICES}" >&2
  exit 1
fi

cd "$PROJECT_ROOT/$SERVICE_PATH"

# Allow callers to pass through dotnet run args (e.g., --urls http://localhost:5004)
exec dotnet run "$@"
