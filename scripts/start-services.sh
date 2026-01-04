#!/bin/zsh
# Start all microservices with environment variables loaded

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_ROOT"

# Load environment variables
source scripts/load-env.sh

# Kill any existing dotnet processes
echo "Stopping existing services..."
pkill -f "dotnet run" 2>/dev/null || true
sleep 2

# Start services
echo "Starting Identity Service..."
cd "$PROJECT_ROOT/src/Services/Identity/PersonalUniverse.Identity.API"
dotnet run > /tmp/identity-service.log 2>&1 &
IDENTITY_PID=$!

echo "Starting Storage Service..."
cd "$PROJECT_ROOT/src/Services/Storage/PersonalUniverse.Storage.API"
dotnet run > /tmp/storage-service.log 2>&1 &
STORAGE_PID=$!

echo "Starting Personality Processing Service..."
cd "$PROJECT_ROOT/src/Services/PersonalityProcessing/PersonalUniverse.PersonalityProcessing.API"
dotnet run > /tmp/personality-service.log 2>&1 &
PERSONALITY_PID=$!

echo "Starting Simulation Engine..."
cd "$PROJECT_ROOT/src/Services/SimulationEngine/PersonalUniverse.SimulationEngine.API"
dotnet run > /tmp/simulation-service.log 2>&1 &
SIMULATION_PID=$!

echo "Starting Event Service..."
cd "$PROJECT_ROOT/src/Services/EventService/PersonalUniverse.EventService.API"
dotnet run > /tmp/event-service.log 2>&1 &
EVENT_PID=$!

echo "Starting Visualization Feed Service..."
cd "$PROJECT_ROOT/src/Services/VisualizationFeed/PersonalUniverse.VisualizationFeed.API"
dotnet run > /tmp/visualization-service.log 2>&1 &
VISUALIZATION_PID=$!

echo ""
echo "Services starting..."
echo "Identity PID: $IDENTITY_PID"
echo "Storage PID: $STORAGE_PID"
echo "Personality PID: $PERSONALITY_PID"
echo "Simulation PID: $SIMULATION_PID"
echo "Event PID: $EVENT_PID"
echo "Visualization PID: $VISUALIZATION_PID"
echo ""
echo "Waiting 15 seconds for services to start..."
sleep 15

echo ""
echo "Checking service health..."
echo "Identity: $(curl -s http://localhost:5169/health 2>&1 | head -c 50)"
echo "Storage: $(curl -s http://localhost:5206/health 2>&1 | head -c 50)"
echo ""
echo "Log files:"
echo "  - /tmp/identity-service.log"
echo "  - /tmp/storage-service.log"
echo "  - /tmp/personality-service.log"
echo "  - /tmp/simulation-service.log"
echo "  - /tmp/event-service.log"
echo "  - /tmp/visualization-service.log"
