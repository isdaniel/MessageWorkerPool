using System;
using System.Threading.Tasks;
using MessageWorkerPool.Utilities;
using MessageWorkerPool.Telemetry.Abstractions;
using MessageWorkerPool.OpenTelemetry.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using OpenTelemetry;
using OpenTelemetry.Resources;

namespace WorkerClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Initialize OpenTelemetry for the worker client
            var serviceCollection = new ServiceCollection();

            var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";

            serviceCollection.AddMessageWorkerPoolTelemetry(options =>
             {
                 options.ServiceName = "MessageWorkerPool.Example.Client";
                 options.ServiceVersion = "1.0.0";
                 options.EnableRuntimeInstrumentation = true;
                 options.ServiceInstanceId = Environment.GetEnvironmentVariable("OTEL_SERVICE_INSTANCE_ID");

                 // Configure tracing with OTLP exporter
                 options.ConfigureTracing = tracing =>
                 {
                     tracing.AddOtlpExporter(otlpOptions =>
                     {
                         otlpOptions.Endpoint = new Uri(otlpEndpoint);
                         otlpOptions.ExportProcessorType = ExportProcessorType.Batch;
                     });
                 };
             });


            using var serviceProvider = serviceCollection.BuildServiceProvider();
            _ = serviceProvider.GetService<TracerProvider>();

            // Get the telemetry manager from DI
            var telemetryManager = serviceProvider.GetRequiredService<ITelemetryManager>();


            MessageProcessor processor = new MessageProcessor();
            await processor.InitialAsync();

            await processor.DoWorkAsync(async (task, token) => {

                // Start activity with parent context from task headers
                // The TelemetryManager will extract the trace context from headers automatically
                using var activity = telemetryManager.StartTaskProcessingActivity(
                    "worker-client",
                    task.OriginalQueueName ?? "unknown",
                    task.CorrelationId,
                    task.Headers);

                activity?.SetTag("message.content", task.Message);
                activity?.SetTag("worker.client", "example");

                Console.WriteLine($"Processing task with message: {task.Message}, Sleeping 1s".ToIgnoreMessage());

                await Task.Delay(1000,token).ConfigureAwait(false);

                activity?.SetTag("processing.result", "success");
                telemetryManager.SetTaskStatus(activity, MessageStatus.MESSAGE_DONE);
                return new MessageOutputTask()
                {
                    Message = "Processed successfully!",
                    Status = MessageStatus.MESSAGE_DONE
                };
            }).ConfigureAwait(false);
        }
    }
}
