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
using MessageWorkerPool.OpenTelemetry;

// Configure OpenTelemetry in your service
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMessageWorkerPoolMetrics();
    })
    .WithTracing(tracing =>
    {
        tracing.AddMessageWorkerPoolTracing();
    });
```

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
