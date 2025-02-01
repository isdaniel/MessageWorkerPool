#!/bin/bash

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

set_env_from_file "./env/.env"
set_env_from_file "./env/.local_env"
echo "127.0.0.1 kafka1" | sudo tee -a /etc/hosts
for i in {1..5}
do
    docker start $(docker ps -a --filter "name=envpreparatory" --format "{{.ID}}")
    echo "Waiting for 5 seconds before running .NET testing iteration $i"
    sleep 5
    echo "Running .NET testing iteration $i"
    dotnet test IntegrationTester.kafka.sln --configuration Release --logger:"console;verbosity=detailed"
done
