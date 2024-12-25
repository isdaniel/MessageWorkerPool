#!/bin/bash

toggle_container_state() {
    local container_id="$1"
    local status="$2"

    if [[ "$status" == Up* ]]; then
        echo "Container is running. Stopping container..."
        if docker stop "$container_id"; then
            echo "Container stopped successfully."
        else
            echo "Failed to stop container: $container_id"
        fi
        sleep_in_range 15 30
    elif [[ "$status" == Exited* ]]; then
        echo "Container is stopped. Starting container..."
        if docker start "$container_id"; then
            echo "Container started successfully."
        else
            echo "Failed to start container: $container_id"
        fi
        sleep_in_range 25 50
    else
        echo "Unknown container state: $status"
    fi
}

# Function to sleep for a random duration within a range
sleep_in_range() {
    local min="$1"
    local max="$2"
    local sleep_time=$((RANDOM % (max - min + 1) + min))
    echo "Sleeping for $sleep_time seconds..."
    sleep "$sleep_time"
}

# Function to manage container operations in a loop
manage_container() {
    local container_name="$1"

    while true; do
        echo "Checking container status..."
        local container_info
        container_info=$(docker ps -a --filter "name=$container_name" --format "{{.ID}} {{.Status}} {{.Names}}")

        if [ -n "$container_info" ]; then
            echo "Container Info: $container_info"

            # Extract container ID and status
            local container_id status
            container_id=$(echo "$container_info" | awk '{print $1}')
            status=$(echo "$container_info" | awk '{print $2}')

            if [ -n "$status" ]; then
                echo "Container Status: $status"

                # Toggle container state
                toggle_container_state "$container_id" "$status"
            fi
        else
            echo "Container '$container_name' not found. Retrying in 5 seconds..."
            sleep 5
        fi
    done
}

# Main script execution
container_name="$1"

if [ -z "$container_name" ]; then
    echo "Usage: $0 <container_name>"
    exit 1
fi

manage_container "$container_name"
