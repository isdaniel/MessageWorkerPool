using Microsoft.Extensions.DependencyInjection;
using MessageWorkerPool;
using MessageWorkerPool.RabbitMq;
using Microsoft.Extensions.Logging;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.OpenTelemetry.Extensions;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Console;
using Microsoft.AspNetCore.Builder;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;

namespace WorkerHost
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(options => {
                options.FormatterName = ConsoleFormatterNames.Simple;
            });
            builder.Services.Configure<SimpleConsoleFormatterOptions>(options => {
                options.IncludeScopes = true;
                options.TimestampFormat = " yyyy-MM-dd HH:mm:ss ";
            });

            // Add MessageWorkerPool telemetry with OpenTelemetry
            var hostname = Environment.GetEnvironmentVariable("HOSTNAME") ?? System.Net.Dns.GetHostName();
            var instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID") ?? hostname;

            builder.Services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "MessageWorkerPool.Example.Host";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = instanceId;  // Add instance identifier
                options.EnableRuntimeInstrumentation = true;
                options.EnableProcessInstrumentation = true;

                // Configure metrics with OTLP exporter and Prometheus
                options.ConfigureMetrics = metrics =>
                {
                    metrics.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));
                        otlpOptions.Protocol = GetOtlpProtocol();
                    });
                    metrics.AddPrometheusExporter(prometheusOptions =>
                    {
                        prometheusOptions.DisableTotalNameSuffixForCounters = true;
                    });
                };

                // Configure tracing with OTLP exporter
                options.ConfigureTracing = tracing =>
                {
                    tracing.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));
                        otlpOptions.Protocol = GetOtlpProtocol();
                    });
                };
            });

            // Add RabbitMQ Worker Pool
            builder.Services.AddRabbitMqWorkerPool(new RabbitMqSetting
            {
                UserName = Environment.GetEnvironmentVariable("USERNAME") ?? "guest",
                Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "localhost",
                Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort p) ? p : (ushort) 5672,
                PrefetchTaskCount = 3
            }, new WorkerPoolSetting() {
                WorkerUnitCount = 5,
                CommandLine = "dotnet",
                Arguments = @"./WorkerClient/WorkerClient.dll",
                QueueName = Environment.GetEnvironmentVariable("QUEUENAME") ?? "worker-queue",
            });

            var app = builder.Build();

            // Map Prometheus metrics endpoint
            app.MapPrometheusScrapingEndpoint();

            // Configure URLs for metrics endpoint
            app.Urls.Add("http://*:9464");

            await app.RunAsync();
        }

        /// <summary>
        /// Parses the OTLP protocol from the OTEL_EXPORTER_OTLP_PROTOCOL environment variable.
        /// </summary>
        /// <returns>The OTLP export protocol (defaults to Grpc if not specified or invalid).</returns>
        private static OtlpExportProtocol GetOtlpProtocol()
        {
            var protocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
            return protocol?.ToLower() switch
            {
                "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
                "grpc" => OtlpExportProtocol.Grpc,
                _ => OtlpExportProtocol.Grpc // Default
            };
        }
    }
}
