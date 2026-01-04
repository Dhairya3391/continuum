#!/bin/bash

# Run all microservices in separate terminal tabs/windows
# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}Starting Personal Universe Simulator - All Services${NC}"

# Check if .env exists
if [ ! -f .env ]; then
    echo -e "${RED}Error: .env file not found. Copy .env.example to .env first.${NC}"
    exit 1
fi

# Load environment variables
export $(cat .env | grep -v '^#' | xargs)

# Base directory
BASE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Kill any existing services on these ports
echo -e "${YELLOW}Checking for existing services...${NC}"
lsof -ti:5001,5002,5003,5004,5005,5006 | xargs kill -9 2>/dev/null || true

# Function to run service in background
run_service() {
    local service_name=$1
    local service_path=$2
    local port=$3
    
    echo -e "${GREEN}Starting $service_name on port $port...${NC}"
    cd "$BASE_DIR/$service_path"
    dotnet run --urls "http://localhost:$port" > "$BASE_DIR/logs/$service_name.log" 2>&1 &
    echo $! > "$BASE_DIR/logs/$service_name.pid"
}

# Create logs directory
mkdir -p "$BASE_DIR/logs"

# Start all services
run_service "Identity" "src/Services/Identity/PersonalUniverse.Identity.API" 5001
sleep 2
run_service "Storage" "src/Services/Storage/PersonalUniverse.Storage.API" 5002
sleep 2
run_service "Personality" "src/Services/PersonalityProcessing/PersonalUniverse.PersonalityProcessing.API" 5003
sleep 2
run_service "Simulation" "src/Services/SimulationEngine/PersonalUniverse.SimulationEngine.API" 5004
sleep 2
run_service "Events" "src/Services/EventService/PersonalUniverse.EventService.API" 5005
sleep 2
run_service "Visualization" "src/Services/VisualizationFeed/PersonalUniverse.VisualizationFeed.API" 5006

echo -e "${GREEN}All services started!${NC}"
echo -e "${YELLOW}Check logs in: $BASE_DIR/logs/${NC}"
echo ""
echo -e "${GREEN}Service URLs:${NC}"
echo "  - Identity:       http://localhost:5001/scalar"
echo "  - Storage:        http://localhost:5002/scalar"
echo "  - Personality:    http://localhost:5003/scalar"
echo "  - Simulation:     http://localhost:5004/scalar"
echo "  - Events:         http://localhost:5005/scalar"
echo "  - Visualization:  http://localhost:5006/scalar"
echo ""
echo -e "${YELLOW}To stop all services, run: ./scripts/stop-all.sh${NC}"
echo -e "${YELLOW}To view logs, run: tail -f logs/<service-name>.log${NC}"
