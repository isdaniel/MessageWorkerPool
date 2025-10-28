using System;
using System.Collections.Generic;
using System.Diagnostics;
using MessageWorkerPool.Telemetry.Abstractions;

namespace MessageWorkerPool.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry implementation of ITelemetryProvider.
    /// </summary>
    public class OpenTelemetryProvider : ITelemetryProvider, IDisposable
    {
        private readonly ActivitySource _activitySource;
        private readonly OpenTelemetryMetrics _metrics;

        /// <summary>
        /// Initializes a new instance of OpenTelemetryProvider.
        /// </summary>
        /// <param name="serviceName">The service name for telemetry.</param>
        /// <param name="serviceVersion">The service version for telemetry.</param>
        public OpenTelemetryProvider(string serviceName = "MessageWorkerPool", string serviceVersion = "1.0.0")
        {
            _activitySource = new ActivitySource(serviceName, serviceVersion);
            _metrics = new OpenTelemetryMetrics(serviceName, serviceVersion);
        }

        /// <inheritdoc />
        public IActivity StartActivity(
            string operationName,
            IDictionary<string, object> tags = null,
            ActivityKind kind = ActivityKind.Internal,
            ActivityContext parentContext = default)
        {
            Activity activity;

            if (parentContext != default)
            {
                // Start activity with explicit parent context for distributed tracing
                activity = _activitySource.StartActivity(operationName, kind, parentContext);
            }
            else
            {
                // Start activity with implicit parent (current activity)
                activity = _activitySource.StartActivity(operationName, kind);
            }

            if (activity == null)
                return null;

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    activity.SetTag(tag.Key, tag.Value?.ToString());
                }
            }

            return new OpenTelemetryActivity(activity);
        }

        /// <inheritdoc />
        public IMetrics Metrics => _metrics;

        /// <summary>
        /// Disposes the telemetry provider.
        /// </summary>
        public void Dispose()
        {
            _activitySource?.Dispose();
            _metrics?.Dispose();
        }
    }
}
