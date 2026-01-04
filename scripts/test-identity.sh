#!/bin/bash
cd /Users/dhairya/Development/continuum

echo "Starting Identity Service..."
dotnet run --project src/Services/Identity/PersonalUniverse.Identity.API/PersonalUniverse.Identity.API.csproj --urls "http://localhost:5001" &
PID=$!
echo "Identity Service PID: $PID"

echo "Waiting for service to start..."
for i in {1..30}; do
    if curl -s http://localhost:5001/health > /dev/null 2>&1; then
        echo "✓ Identity Service is running on http://localhost:5001"
        echo "✓ Scalar UI: http://localhost:5001/scalar/v1"
        echo "✓ Health: http://localhost:5001/health"
        exit 0
    fi
    echo -n "."
done

echo "✗ Identity Service failed to start"
kill $PID 2>/dev/null
exit 1
