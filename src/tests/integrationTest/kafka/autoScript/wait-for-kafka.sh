#!/bin/bash

# wait-for-kafka.sh - Wait for Kafka to be fully ready before running tests

set -e

KAFKA_HOST="${KAFKA_HOST:-127.0.0.1}"
KAFKA_PORT="${KAFKA_PORT:-9092}"
TIMEOUT="${TIMEOUT:-120}"
RETRY_INTERVAL=5

echo "Waiting for Kafka at $KAFKA_HOST:$KAFKA_PORT to be ready..."

# Function to check if Kafka is ready
check_kafka_ready() {
    # Try to list topics using docker exec into the kafka container
    if docker exec kafka1 kafka-topics --bootstrap-server localhost:9092 --list > /dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Function to check if Kafka port is accessible from host
check_kafka_port() {
    if timeout 5 bash -c "cat < /dev/null > /dev/tcp/$KAFKA_HOST/$KAFKA_PORT" 2>/dev/null; then
        return 0
    else
        return 1
    fi
}

# Wait for Kafka port to be accessible
elapsed=0
echo "Step 1: Waiting for Kafka port $KAFKA_PORT to be accessible..."
while [ $elapsed -lt $TIMEOUT ]; do
    if check_kafka_port; then
        echo "✓ Kafka port $KAFKA_PORT is accessible"
        break
    fi
    echo "Kafka port not yet accessible, waiting... (${elapsed}s/${TIMEOUT}s)"
    sleep $RETRY_INTERVAL
    elapsed=$((elapsed + RETRY_INTERVAL))
done

if [ $elapsed -ge $TIMEOUT ]; then
    echo "✗ Timeout waiting for Kafka port to be accessible"
    exit 1
fi

# Wait for Kafka to be fully ready (able to handle topic operations)
elapsed=0
echo "Step 2: Waiting for Kafka broker to be fully ready..."
while [ $elapsed -lt $TIMEOUT ]; do
    if check_kafka_ready; then
        echo "✓ Kafka broker is ready and responsive"

        # Additional check: try to create a test topic
        echo "Step 3: Verifying Kafka can create topics..."
        if docker exec kafka1 kafka-topics --bootstrap-server localhost:9092 \
            --create --if-not-exists --topic kafka-health-check \
            --partitions 1 --replication-factor 1 > /dev/null 2>&1; then
            echo "✓ Kafka is fully operational (topic creation successful)"

            # Clean up test topic
            docker exec kafka1 kafka-topics --bootstrap-server localhost:9092 \
                --delete --topic kafka-health-check > /dev/null 2>&1 || true

            echo ""
            echo "==================================="
            echo "Kafka is ready for testing!"
            echo "==================================="
            exit 0
        fi
    fi
    echo "Kafka not fully ready yet, waiting... (${elapsed}s/${TIMEOUT}s)"
    sleep $RETRY_INTERVAL
    elapsed=$((elapsed + RETRY_INTERVAL))
done

echo "✗ Timeout waiting for Kafka to be fully ready"
echo "Please check Kafka logs: docker logs kafka1"
exit 1
