#!/bin/zsh
# Load environment variables from .env file
# Handles complex values with semicolons and special characters

ENV_FILE="${1:-.env}"

if [ ! -f "$ENV_FILE" ]; then
    echo "Error: $ENV_FILE not found"
    exit 1
fi

# Read .env file line by line
while IFS= read -r line || [ -n "$line" ]; do
    # Skip comments and empty lines
    [[ "$line" =~ ^#.*$ ]] && continue
    [[ -z "$line" ]] && continue
    
    # Extract key and value (everything after first =)
    if [[ "$line" == *"="* ]]; then
        key="${line%%=*}"
        value="${line#*=}"
        
        # Only export if key is valid
        if [[ "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]]; then
            export "$key"="$value"
        fi
    fi
done < "$ENV_FILE"

echo "Environment variables loaded from $ENV_FILE"
