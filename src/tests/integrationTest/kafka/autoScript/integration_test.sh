#!/bin/bash

set -e  # Exit on error

set_env_from_file() {
    local file_path="$1"

    if [[ -f "$file_path" ]]; then
        while IFS='=' read -r key value; do
            # Ignore empty lines and lines starting with #
            if [[ -n "$key" && "$key" != \#* ]]; then
                key=$(echo "$key" | xargs) # Trim whitespace
                value=$(echo "$value" | xargs) # Trim whitespace

                # Remove surrounding quotes if they exist
                if [[ "$value" =~ ^\"(.*)\"$ ]]; then
                    value="${BASH_REMATCH[1]}"
                fi

                export "$key=$value"
                echo "Set environment variable: $key=$value"
            fi
        done < "$file_path"
    else
        echo "Error: $file_path not found."
    fi
}

echo "==================================="
echo "Setting up environment variables..."
echo "==================================="
set_env_from_file "./env/.env"
set_env_from_file "./env/.local_env"

echo ""
echo "==================================="
echo "Configuring host resolution..."
echo "==================================="
# Add kafka1 hostname to /etc/hosts BEFORE waiting for Kafka
# This ensures proper hostname resolution when connecting from the test host
if ! grep -q "kafka1" /etc/hosts; then
    echo "127.0.0.1 kafka1" | sudo tee -a /etc/hosts
    echo "✓ Added kafka1 to /etc/hosts"
else
    echo "✓ kafka1 already in /etc/hosts"
fi

echo ""
echo "==================================="
echo "Waiting for Kafka to be ready..."
echo "==================================="
# Wait for Kafka to be fully operational before running tests
chmod +x ./autoScript/wait-for-kafka.sh
./autoScript/wait-for-kafka.sh

echo ""
echo "==================================="
echo "Starting integration tests..."
echo "==================================="
for i in {1..5}
do
    echo ""
    echo "--- Iteration $i/5 ---"

    # Restart envpreparatory container to ensure fresh state
    docker start $(docker ps -a --filter "name=envpreparatory" --format "{{.ID}}")

    echo "Waiting for 5 seconds before running .NET testing iteration $i"
    sleep 5

    echo "Running .NET testing iteration $i"
    if ! dotnet test IntegrationTester.kafka.sln --configuration Release --logger:"console;verbosity=detailed"; then
        echo "✗ Test iteration $i failed"
        exit 1
    fi
    echo "✓ Test iteration $i completed successfully"
done

echo ""
echo "==================================="
echo "All integration tests completed!"
echo "==================================="
