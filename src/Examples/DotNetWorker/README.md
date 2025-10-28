# .NET Worker Example

This example demonstrates how to use the MessageWorkerPool library with direct project references instead of NuGet package references.

## Structure

- **WorkerHost**: The main application that sets up and manages the worker pool
- **WorkerClient**: The worker process that handles individual tasks

## Building and Running

### Local Development

1. Build the solution:
   ```powershell
   cd src
   dotnet build
   ```

2. Run the WorkerHost:
   ```powershell
   cd src/Examples/DotNetWorker/WorkerHost
   dotnet run
   ```

### Docker

1. Build the Docker image:
   ```powershell
   cd src/Examples/DotNetWorker
   docker build -t messageworkerpool-example .
   ```

2. Run with Docker Compose (if you have RabbitMQ setup):
   ```powershell
   docker run --network host messageworkerpool-example
   ```

## Configuration

The worker pool can be configured via environment variables:

- `USERNAME`: RabbitMQ username (default: guest)
- `PASSWORD`: RabbitMQ password (default: guest)
- `RABBITMQ_HOSTNAME`: RabbitMQ hostname (default: localhost)
- `RABBITMQ_PORT`: RabbitMQ port (default: 5672)
- `QUEUENAME`: Queue name to process (default: worker-queue)

## Key Differences from examples_worker/dotnet

1. **Direct Project References**: Uses `<ProjectReference>` instead of `<PackageReference>` for MessageWorkerPool
2. **Simplified Build**: The Dockerfile can build directly without needing pre-built binaries
3. **Same Folder Structure**: Maintains clean separation between host and client projects