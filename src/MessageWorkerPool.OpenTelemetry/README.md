# MessageWorkerPool.OpenTelemetry

OpenTelemetry extensions for MessageWorkerPool providing comprehensive observability with metrics and distributed tracing capabilities.

## Features

- **Metrics**: Monitor worker pool performance, message processing rates, and resource utilization
- **Distributed Tracing**: Track message flow across worker processes with context propagation
- **OpenTelemetry Integration**: Seamless integration with OpenTelemetry collectors and exporters
- **Prometheus Support**: Built-in Prometheus HTTP listener exporter
- **OTLP Support**: Export telemetry data using OpenTelemetry Protocol

## Installation

```bash
dotnet add package MessageWorkerPool.OpenTelemetry
```

## Usage

Add OpenTelemetry to your MessageWorkerPool application:

```csharp
using MessageWorkerPool.OpenTelemetry.Extensions;

builder.Services.AddMessageWorkerPoolTelemetry(options =>
{
    options.ServiceName = "MyWorkerService";
    options.ServiceVersion = "1.0.0";

    // Configure metrics with OTLP exporter
    options.ConfigureMetrics = metrics =>
    {
        metrics.AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri("http://otel-collector:4317");
        });
    };
});
```

### Custom Service Instance ID

By default, OpenTelemetry generates a random UUID for each service instance. You can customize this to use meaningful identifiers like Docker container names or hostnames:

```csharp
builder.Services.AddMessageWorkerPoolTelemetry(options =>
{
    options.ServiceName = "MyWorkerService";
    options.ServiceVersion = "1.0.0";

    // Option 1: Set custom instance ID directly
    options.ServiceInstanceId = "worker-container-01";

    // Option 2: Use environment variable (recommended for Docker/Kubernetes)
    options.ServiceInstanceId = Environment.GetEnvironmentVariable("OTEL_SERVICE_INSTANCE_ID");

    // Option 3: Leave null to auto-detect (priority order):
    // 1. HOSTNAME environment variable (Docker container name)
    // 2. COMPUTERNAME environment variable (Windows)
    // 3. DNS hostname
    // If all fail, OpenTelemetry SDK generates a UUID
});
```

### Docker Container Setup

To use the container hostname as the instance ID in Docker Compose:

```yaml
services:
  workersample:
    build: ./src/Examples/DotNetWorker
    environment:
      - OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
      # The container name will be used as instance ID
      # Or explicitly set it:
      - OTEL_SERVICE_INSTANCE_ID=${HOSTNAME}
```

When you scale your service, each container will have a unique instance ID:

```bash
docker-compose up --scale workersample=3
```

This will create instances like:
- `workersample-1` → `instance="workersample-1"`
- `workersample-2` → `instance="workersample-2"`
- `workersample-3` → `instance="workersample-3"`

## Available Metrics

The package provides various metrics including:
- Message processing rate
- Worker pool utilization
- Message queue depth
- Processing duration
- Error rates

## Distributed Tracing

Trace context is automatically propagated across worker processes, enabling end-to-end visibility of message processing flows.

## Documentation

For more information, visit the [MessageWorkerPool repository](https://github.com/isdaniel/MessageWorkerPool).
