using System;
using System.Collections.Generic;
using System.Diagnostics;
using MessageWorkerPool.Telemetry.Abstractions;
using MessageWorkerPool.Utilities;

namespace MessageWorkerPool.Telemetry
{
    /// <summary>
    /// Central telemetry manager for WorkerPool operations.
    /// Provides a unified interface for all telemetry activities.
    /// </summary>
    public static class TelemetryManager
    {
        private static ITelemetryProvider _provider = NoOpTelemetryProvider.Instance;
        private static Func<IDictionary<string, object>, ActivityContext> _contextExtractor;

        /// <summary>
        /// Sets the telemetry provider to use.
        /// </summary>
        /// <param name="provider">The telemetry provider.</param>
        public static void SetProvider(ITelemetryProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Sets a custom trace context extractor for extracting parent context from message headers.
        /// </summary>
        /// <param name="extractor">Function to extract ActivityContext from message headers.</param>
        public static void SetTraceContextExtractor(Func<IDictionary<string, object>, ActivityContext> extractor)
        {
            _contextExtractor = extractor;
        }

        /// <summary>
        /// Gets the current telemetry provider.
        /// </summary>
        public static ITelemetryProvider Provider => _provider;

        /// <summary>
        /// Starts a new activity for worker initialization.
        /// </summary>
        /// <param name="workerId">The worker ID.</param>
        /// <param name="queueName">The queue name.</param>
        /// <returns>An Activity instance or null if not enabled.</returns>
        public static IActivity StartWorkerInitActivity(string workerId, string queueName)
        {
            var activity = _provider.StartActivity("Worker.Init");
            activity?.SetTag("worker.id", workerId);
            activity?.SetTag("queue.name", queueName);
            return activity;
        }

        /// <summary>
        /// Starts a new activity for task processing.
        /// </summary>
        /// <param name="workerId">The worker ID.</param>
        /// <param name="queueName">The queue name.</param>
        /// <param name="correlationId">The correlation ID of the task.</param>
        /// <param name="messageHeaders">Optional message headers for trace context propagation.</param>
        /// <returns>An Activity instance or null if not enabled.</returns>
        public static IActivity StartTaskProcessingActivity(
            string workerId,
            string queueName,
            string correlationId,
            IDictionary<string, object> messageHeaders = null)
        {
            // Extract parent context from message headers if available
            ActivityContext parentContext = default;
            if (messageHeaders != null && _contextExtractor != null)
            {
                try
                {
                    parentContext = _contextExtractor(messageHeaders);
                }
                catch
                {
                    // If context extraction fails, continue without parent context
                }
            }

            // Use ActivityKind.Consumer for message processing activities
            var activity = _provider.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, parentContext);

            // Set individual tags on the activity
            activity?.SetTag("worker.id", workerId);
            activity?.SetTag("queue.name", queueName);
            activity?.SetTag("correlation.id", correlationId);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination", queueName);
            activity?.SetTag("messaging.operation", "process");

            return activity;
        }

        /// <summary>
        /// Starts a new activity for worker pool initialization.
        /// </summary>
        /// <param name="workerCount">The number of workers in the pool.</param>
        /// <param name="queueName">The queue name.</param>
        /// <returns>An Activity instance or null if not enabled.</returns>
        public static IActivity StartPoolInitActivity(int workerCount, string queueName)
        {
            var activity = _provider.StartActivity("WorkerPool.Init");
            activity?.SetTag("workerpool.worker_count", workerCount);
            activity?.SetTag("queue.name", queueName);
            return activity;
        }

        /// <summary>
        /// Starts a new activity for graceful shutdown.
        /// </summary>
        /// <param name="workerId">The worker ID.</param>
        /// <returns>An Activity instance or null if not enabled.</returns>
        public static IActivity StartShutdownActivity(string workerId)
        {
            var activity = _provider.StartActivity("Worker.Shutdown");
            activity?.SetTag("worker.id", workerId);
            return activity;
        }

        /// <summary>
        /// Records an exception in the given activity.
        /// </summary>
        /// <param name="activity">The activity to record the exception in.</param>
        /// <param name="exception">The exception to record.</param>
        public static void RecordException(IActivity activity, Exception exception)
        {
            if (activity == null || exception == null)
                return;

            activity.SetStatus(ActivityStatus.Error, exception.Message);
            activity.RecordException(exception);
        }

        /// <summary>
        /// Sets the status of a task processing activity based on MessageStatus.
        /// </summary>
        /// <param name="activity">The activity to update.</param>
        /// <param name="status">The message status.</param>
        public static void SetTaskStatus(IActivity activity, MessageStatus status)
        {
            if (activity == null)
                return;

            activity.SetTag("message.status", status.ToString());

            switch (status)
            {
                case MessageStatus.MESSAGE_DONE:
                case MessageStatus.MESSAGE_DONE_WITH_REPLY:
                case MessageStatus.IGNORE_MESSAGE:
                    activity.SetStatus(ActivityStatus.Ok);
                    break;
                case MessageStatus.UNKNOWN_ERROR:
                    activity.SetStatus(ActivityStatus.Error, "UNKNOWN_ERROR");
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Gets the metrics recorder.
        /// </summary>
        public static IMetrics Metrics => _provider.Metrics;
    }
}
