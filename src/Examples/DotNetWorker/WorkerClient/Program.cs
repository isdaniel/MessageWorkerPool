using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessageWorkerPool.Utilities;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.OpenTelemetry.Extensions;
using MessageWorkerPool.OpenTelemetry;
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
            serviceCollection.AddMessageWorkerPoolOpenTelemetry("MessageWorkerPool.Example.Client", "1.0.0");

            var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";

            // Configure OpenTelemetry tracing with OTLP exporter
            serviceCollection.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService("MessageWorkerPool.Example.Client", "1.0.0"))
                .WithTracing(tracing =>
                {
                    tracing.AddMessageWorkerPoolInstrumentation("MessageWorkerPool.Example.Client")
                        .AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otlpEndpoint);
                            options.ExportProcessorType = ExportProcessorType.Batch;
                        });
                });


            using var serviceProvider = serviceCollection.BuildServiceProvider();
            _ = serviceProvider.GetService<TracerProvider>();

            // Configure TelemetryManager to extract trace context from message headers
            TelemetryManager.SetTraceContextExtractor(TraceContextPropagation.ExtractTraceContext);


            MessageProcessor processor = new MessageProcessor();
            await processor.InitialAsync();

            await processor.DoWorkAsync(async (task, token) => {

                // Start activity with parent context from task headers
                // The TelemetryManager will extract the trace context from headers automatically
                using var activity = TelemetryManager.StartTaskProcessingActivity(
                    "worker-client",
                    task.OriginalQueueName ?? "unknown",
                    task.CorrelationId,
                    task.Headers);

                activity?.SetTag("message.content", task.Message);
                activity?.SetTag("worker.client", "example");

                Console.WriteLine($"Processing task with message: {task.Message}, Sleeping 1s".ToIgnoreMessage());

                await Task.Delay(1000,token).ConfigureAwait(false);

                activity?.SetTag("processing.result", "success");
                TelemetryManager.SetTaskStatus(activity, MessageStatus.MESSAGE_DONE);
                return new MessageOutputTask()
                {
                    Message = "Processed successfully!",
                    Status = MessageStatus.MESSAGE_DONE
                };
            }).ConfigureAwait(false);
        }
    }
}
