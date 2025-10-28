using System;
using System.Collections.Generic;
using System.Diagnostics;
using MessageWorkerPool.Telemetry.Abstractions;

namespace MessageWorkerPool.Telemetry
{
    /// <summary>
    /// A no-operation telemetry provider that does nothing.
    /// Used when telemetry is disabled.
    /// </summary>
    public class NoOpTelemetryProvider : ITelemetryProvider
    {
        /// <summary>
        /// Singleton instance of the no-op telemetry provider.
        /// </summary>
        public static readonly NoOpTelemetryProvider Instance = new NoOpTelemetryProvider();

        private NoOpTelemetryProvider() { }

        /// <inheritdoc />
        public IActivity StartActivity(
            string operationName,
            IDictionary<string, object> tags = null,
            ActivityKind kind = ActivityKind.Internal,
            ActivityContext parentContext = default)
        {
            return NoOpActivity.Instance;
        }

        /// <inheritdoc />
        public IMetrics Metrics { get; } = NoOpMetrics.Instance;
    }

    /// <summary>
    /// A no-operation activity that does nothing.
    /// </summary>
    public class NoOpActivity : IActivity
    {
        /// <summary>
        /// Singleton instance of the no-op activity.
        /// </summary>
        public static readonly NoOpActivity Instance = new NoOpActivity();

        private NoOpActivity() { }

        /// <inheritdoc />
        public void SetTag(string key, object value) { }

        /// <inheritdoc />
        public void SetTags(IDictionary<string, object> tags) { }

        /// <inheritdoc />
        public void SetStatus(ActivityStatus status, string description = null) { }

        /// <inheritdoc />
        public void RecordException(Exception exception) { }

        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>
    /// A no-operation metrics recorder that does nothing.
    /// </summary>
    public class NoOpMetrics : IMetrics
    {
        /// <summary>
        /// Singleton instance of the no-op metrics recorder.
        /// </summary>
        public static readonly NoOpMetrics Instance = new NoOpMetrics();

        private NoOpMetrics() { }

        /// <inheritdoc />
        public void RecordTaskProcessed(string queueName = null, string workerId = null) { }

        /// <inheritdoc />
        public void RecordTaskFailed(string queueName = null, string workerId = null, string errorType = null) { }

        /// <inheritdoc />
        public void RecordTaskRejected(string queueName = null, string workerId = null) { }

        /// <inheritdoc />
        public void RecordTaskDuration(double durationMs, string queueName = null, string workerId = null) { }

        /// <inheritdoc />
        public void SetActiveWorkers(int count) { }

        /// <inheritdoc />
        public void SetProcessingTasks(int count) { }

        /// <inheritdoc />
        public void SetHealthyWorkers(int count) { }

        /// <inheritdoc />
        public void SetStoppedWorkers(int count) { }

        /// <inheritdoc />
        public void IncrementProcessingTasks() { }

        /// <inheritdoc />
        public void DecrementProcessingTasks() { }
    }
}
