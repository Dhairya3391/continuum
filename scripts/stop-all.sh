#!/bin/bash

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}Stopping all Personal Universe services...${NC}"

BASE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Kill by PID files
if [ -d "$BASE_DIR/logs" ]; then
    for pidfile in "$BASE_DIR/logs"/*.pid; do
        if [ -f "$pidfile" ]; then
            pid=$(cat "$pidfile")
            if ps -p $pid > /dev/null 2>&1; then
                kill $pid
                echo -e "${GREEN}Stopped process $pid${NC}"
            fi
            rm "$pidfile"
        fi
    done
fi

# Kill by ports (backup)
lsof -ti:5001,5002,5003,5004,5005,5006 | xargs kill -9 2>/dev/null || true

echo -e "${GREEN}All services stopped!${NC}"
