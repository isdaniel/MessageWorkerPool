using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MessageWorkerPool.Telemetry.Abstractions
{
    /// <summary>
    /// Abstraction for telemetry providers (OpenTelemetry, Application Insights, etc.)
    /// </summary>
    public interface ITelemetryProvider
    {
        /// <summary>
        /// Creates a new activity for tracing.
        /// </summary>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="tags">Optional tags to add to the activity.</param>
        /// <param name="kind">The kind of activity (Internal, Server, Client, Producer, Consumer).</param>
        /// <param name="parentContext">Optional parent activity context for distributed tracing.</param>
        /// <returns>An activity that should be disposed when the operation completes.</returns>
        IActivity StartActivity(
            string operationName,
            IDictionary<string, object> tags = null,
            ActivityKind kind = ActivityKind.Internal,
            ActivityContext parentContext = default);

        /// <summary>
        /// Records metrics about worker operations.
        /// </summary>
        IMetrics Metrics { get; }
    }
}
