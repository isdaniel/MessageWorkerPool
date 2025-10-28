using System;
using Microsoft.Extensions.DependencyInjection;
using MessageWorkerPool.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MessageWorkerPool.OpenTelemetry.Extensions
{
    /// <summary>
    /// Extension methods for configuring OpenTelemetry with MessageWorkerPool.
    /// </summary>
    public static class MessageWorkerPoolOpenTelemetryExtensions
    {
        /// <summary>
        /// Enables OpenTelemetry telemetry for MessageWorkerPool.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="serviceName">The service name for telemetry.</param>
        /// <param name="serviceVersion">The service version for telemetry.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMessageWorkerPoolOpenTelemetry(
            this IServiceCollection services,
            string serviceName = "MessageWorkerPool",
            string serviceVersion = "1.0.0")
        {
            // Set the OpenTelemetry provider
            var provider = new OpenTelemetryProvider(serviceName, serviceVersion);
            TelemetryManager.SetProvider(provider);

            // Set the trace context extractor for W3C trace context propagation
            TelemetryManager.SetTraceContextExtractor(TraceContextPropagation.ExtractTraceContext);

            return services;
        }

        /// <summary>
        /// Adds MessageWorkerPool metrics to the OpenTelemetry MeterProvider.
        /// </summary>
        /// <param name="builder">The MeterProviderBuilder instance.</param>
        /// <param name="serviceName">The service name for metrics (default: MessageWorkerPool).</param>
        /// <returns>The MeterProviderBuilder for chaining.</returns>
        public static MeterProviderBuilder AddMessageWorkerPoolInstrumentation(
            this MeterProviderBuilder builder,
            string serviceName = "MessageWorkerPool")
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            return builder.AddMeter(serviceName);
        }

        /// <summary>
        /// Adds MessageWorkerPool tracing to the OpenTelemetry TracerProvider.
        /// </summary>
        /// <param name="builder">The TracerProviderBuilder instance.</param>
        /// <param name="serviceName">The service name for tracing (default: MessageWorkerPool).</param>
        /// <returns>The TracerProviderBuilder for chaining.</returns>
        public static TracerProviderBuilder AddMessageWorkerPoolInstrumentation(
            this TracerProviderBuilder builder,
            string serviceName = "MessageWorkerPool")
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            return builder.AddSource(serviceName);
        }

        /// <summary>
        /// Configures OpenTelemetry with all MessageWorkerPool instrumentation and common settings.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration action.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMessageWorkerPoolTelemetry(
            this IServiceCollection services,
            Action<MessageWorkerPoolTelemetryOptions> configure = null)
        {
            var options = new MessageWorkerPoolTelemetryOptions();
            configure?.Invoke(options);

            // Set the OpenTelemetry provider
            var provider = new OpenTelemetryProvider(options.ServiceName, options.ServiceVersion);
            TelemetryManager.SetProvider(provider);

            // Set the trace context extractor for W3C trace context propagation
            TelemetryManager.SetTraceContextExtractor(TraceContextPropagation.ExtractTraceContext);

            // Configure OpenTelemetry
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(options.ServiceName, options.ServiceVersion))
                .WithMetrics(metrics =>
                {
                    metrics.AddMessageWorkerPoolInstrumentation(options.ServiceName);

                    if (options.EnableRuntimeInstrumentation)
                        metrics.AddRuntimeInstrumentation();

                    if (options.EnableProcessInstrumentation)
                        metrics.AddProcessInstrumentation();

                    options.ConfigureMetrics?.Invoke(metrics);
                })
                .WithTracing(tracing =>
                {
                    tracing.AddMessageWorkerPoolInstrumentation(options.ServiceName);
                    options.ConfigureTracing?.Invoke(tracing);
                });

            return services;
        }
    }

    /// <summary>
    /// Configuration options for MessageWorkerPool telemetry.
    /// </summary>
    public class MessageWorkerPoolTelemetryOptions
    {
        /// <summary>
        /// Gets or sets the service name for telemetry.
        /// </summary>
        public string ServiceName { get; set; } = "MessageWorkerPool";

        /// <summary>
        /// Gets or sets the service version for telemetry.
        /// </summary>
        public string ServiceVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets whether to enable runtime instrumentation.
        /// </summary>
        public bool EnableRuntimeInstrumentation { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable process instrumentation.
        /// </summary>
        public bool EnableProcessInstrumentation { get; set; } = true;

        /// <summary>
        /// Gets or sets an action to configure metrics.
        /// </summary>
        public Action<MeterProviderBuilder> ConfigureMetrics { get; set; }

        /// <summary>
        /// Gets or sets an action to configure tracing.
        /// </summary>
        public Action<TracerProviderBuilder> ConfigureTracing { get; set; }
    }
}
