using System;
using System.Collections.Generic;
using MessageWorkerPool.Utilities;

namespace MessageWorkerPool.Telemetry.Abstractions
{
    /// <summary>
    /// Interface for managing telemetry operations in WorkerPool.
    /// Provides a unified interface for all telemetry activities.
    /// </summary>
    public interface ITelemetryManager
    {
        /// <summary>
        /// Gets the current telemetry provider.
        /// </summary>
        ITelemetryProvider Provider { get; }

        /// <summary>
        /// Gets the metrics recorder.
        /// </summary>
        IMetrics Metrics { get; }

        /// <summary>
        /// Starts a new activity for worker initialization.
        /// </summary>
        /// <param name="workerId">The worker ID.</param>
        /// <param name="queueName">The queue name.</param>
        /// <returns>An Activity instance or null if not enabled.</returns>
        IActivity StartWorkerInitActivity(string workerId, string queueName);

        /// <summary>
        /// Starts a new activity for task processing.
        /// </summary>
        /// <param name="workerId">The worker ID.</param>
        /// <param name="queueName">The queue name.</param>
        /// <param name="correlationId">The correlation ID of the task.</param>
        /// <param name="messageHeaders">Optional message headers for trace context propagation.</param>
        /// <returns>An Activity instance or null if not enabled.</returns>
        IActivity StartTaskProcessingActivity(
            string workerId,
            string queueName,
            string correlationId,
            IDictionary<string, object> messageHeaders = null);

        /// <summary>
        /// Starts a new activity for task processing (string dictionary variant).
        /// </summary>
        /// <param name="workerId">The worker ID.</param>
        /// <param name="queueName">The queue name.</param>
        /// <param name="correlationId">The correlation ID of the task.</param>
        /// <param name="messageHeaders">Optional message headers for trace context propagation.</param>
        /// <returns>An Activity instance or null if not enabled.</returns>
        IActivity StartTaskProcessingActivity(
            string workerId,
            string queueName,
            string correlationId,
            IDictionary<string, string> messageHeaders);

        /// <summary>
        /// Starts a new activity for worker pool initialization.
        /// </summary>
        /// <param name="workerCount">The number of workers in the pool.</param>
        /// <param name="queueName">The queue name.</param>
        /// <returns>An Activity instance or null if not enabled.</returns>
        IActivity StartPoolInitActivity(int workerCount, string queueName);

        /// <summary>
        /// Starts a new activity for graceful shutdown.
        /// </summary>
        /// <param name="workerId">The worker ID.</param>
        /// <returns>An Activity instance or null if not enabled.</returns>
        IActivity StartShutdownActivity(string workerId);

        /// <summary>
        /// Records an exception in the given activity.
        /// </summary>
        /// <param name="activity">The activity to record the exception in.</param>
        /// <param name="exception">The exception to record.</param>
        void RecordException(IActivity activity, Exception exception);

        /// <summary>
        /// Sets the status of a task processing activity based on MessageStatus.
        /// </summary>
        /// <param name="activity">The activity to update.</param>
        /// <param name="status">The message status.</param>
        void SetTaskStatus(IActivity activity, MessageStatus status);
    }
}
