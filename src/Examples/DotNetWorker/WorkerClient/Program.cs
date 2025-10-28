using System;
using System.Threading;
using System.Threading.Tasks;
using MessageWorkerPool.Utilities;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.OpenTelemetry.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace WorkerClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Initialize OpenTelemetry for the worker client using the new extension
            var serviceCollection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            serviceCollection.AddMessageWorkerPoolOpenTelemetry("MessageWorkerPool.Example.Client", "1.0.0");

            // Configure OpenTelemetry manually for this simple client
            serviceCollection.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.AddMessageWorkerPoolInstrumentation("MessageWorkerPool.Example.Client")
                        .AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));
                        });
                });

            using var serviceProvider = serviceCollection.BuildServiceProvider();

            MessageProcessor processor = new MessageProcessor();
            await processor.InitialAsync();
            await processor.DoWorkAsync(async (task, token) => {
                using var activity = TelemetryManager.StartTaskProcessingActivity("worker-client", "default", task.CorrelationId);
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
