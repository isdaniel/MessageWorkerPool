using System;
using System.Diagnostics;
using MessageWorkerPool.Utilities;
using MessageWorkerPool.Telemetry.Abstractions;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool.Telemetry
{
    /// <summary>
    /// Encapsulates telemetry recording for task processing using AOP principles.
    /// Handles metrics, activity tracking, and logging for a single task lifecycle.
    /// </summary>
    public sealed class TaskProcessingTelemetry : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly IActivity _activity;
        private readonly ILogger _logger;
        private readonly string _queueName;
        private readonly string _workerId;
        private bool _disposed;
        private bool _recorded;

        /// <summary>
        /// Initializes a new instance of TaskProcessingTelemetry.
        /// </summary>
        /// <param name="workerId">The worker ID processing the task.</param>
        /// <param name="queueName">The queue name from which the task originated.</param>
        /// <param name="correlationId">The correlation ID of the task.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="messageHeaders">Optional message headers for trace context propagation.</param>
        public TaskProcessingTelemetry(
            string workerId,
            string queueName,
            string correlationId,
            ILogger logger,
            System.Collections.Generic.IDictionary<string, object> messageHeaders = null)
        {
            _workerId = workerId;
            _queueName = queueName;
            _logger = logger;

            _stopwatch = Stopwatch.StartNew();
            _activity = TelemetryManager.StartTaskProcessingActivity(workerId, queueName, correlationId, messageHeaders);

            TelemetryManager.Metrics?.IncrementProcessingTasks();
        }

        /// <summary>
        /// Sets additional tags on the activity.
        /// </summary>
        public void SetTag(string key, object value)
        {
            _activity?.SetTag(key, value);
        }

        /// <summary>
        /// Records a successful task completion.
        /// </summary>
        /// <param name="status">The message status.</param>
        public void RecordSuccess(MessageStatus status)
        {
            if (_recorded) return;

            _stopwatch.Stop();
            TelemetryManager.Metrics?.RecordTaskProcessed(_queueName, _workerId);
            TelemetryManager.Metrics?.RecordTaskDuration(_stopwatch.Elapsed.TotalMilliseconds, _queueName, _workerId);
            TelemetryManager.SetTaskStatus(_activity, status);

            _recorded = true;
        }

        /// <summary>
        /// Records a rejected task.
        /// </summary>
        /// <param name="status">The message status.</param>
        public void RecordRejection(MessageStatus status)
        {
            if (_recorded) return;

            _stopwatch.Stop();
            TelemetryManager.Metrics?.RecordTaskRejected(_queueName, _workerId);
            TelemetryManager.Metrics?.RecordTaskDuration(_stopwatch.Elapsed.TotalMilliseconds, _queueName, _workerId);
            TelemetryManager.SetTaskStatus(_activity, status);

            _recorded = true;
        }

        /// <summary>
        /// Records a failed task with exception details.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        public void RecordFailure(Exception exception)
        {
            if (_recorded) return;

            _stopwatch.Stop();
            TelemetryManager.Metrics?.RecordTaskFailed(_queueName, _workerId, exception.GetType().Name);
            TelemetryManager.Metrics?.RecordTaskDuration(_stopwatch.Elapsed.TotalMilliseconds, _queueName, _workerId);
            TelemetryManager.RecordException(_activity, exception);

            _logger?.LogWarning(exception, "Processing message encountered an exception!");

            _recorded = true;
        }

        /// <summary>
        /// Disposes the telemetry context and decrements processing tasks counter.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            TelemetryManager.Metrics?.DecrementProcessingTasks();
            _activity?.Dispose();
            _disposed = true;
        }
    }
}
