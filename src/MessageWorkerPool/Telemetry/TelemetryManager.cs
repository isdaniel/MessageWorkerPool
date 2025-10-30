using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MessageWorkerPool.Extensions;
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
        private static Func<IDictionary<string, object>, ActivityContext> _contextExtractor = TraceContextPropagation.ExtractTraceContext;

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
                    // If extraction fails, use default context
                    parentContext = default;
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
        /// Starts a new activity for task processing (string dictionary variant).
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
            IDictionary<string, string> messageHeaders)
        {
            // Convert to object dictionary and call the main overload
            IDictionary<string, object> objectHeaders = null;
            if (messageHeaders != null && messageHeaders.Count > 0)
            {
                objectHeaders = new Dictionary<string, object>();
                foreach (var kvp in messageHeaders)
                {
                    objectHeaders[kvp.Key] = kvp.Value;
                }
            }

            return StartTaskProcessingActivity(workerId, queueName, correlationId, objectHeaders);
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

    /// <summary>
    /// Helper class for propagating trace context through messaging systems.
    /// Supports W3C Trace Context standard (traceparent and tracestate).
    /// </summary>
    public static class TraceContextPropagation
    {
        private const string TraceParentHeaderName = "traceparent";
        private const string TraceStateHeaderName = "tracestate";

        /// <summary>
        /// Extracts parent activity context from message headers.
        /// </summary>
        /// <param name="headers">Message headers dictionary.</param>
        /// <returns>ActivityContext if trace context is found, otherwise default.</returns>
        public static ActivityContext ExtractTraceContext(IDictionary<string, object> headers)
        {
            if (headers == null || !headers.Any())
                return default;

            // Try to get traceparent header
            if (!headers.TryGetValue(TraceParentHeaderName, out var traceParentObj))
                return default;

            string traceParent = ConvertToString(traceParentObj);
            if (string.IsNullOrWhiteSpace(traceParent))
                return default;

            // Parse W3C traceparent format: version-traceId-spanId-traceFlags
            // Example: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
            var parts = traceParent.Split('-');
            if (parts.Length != 4)
                return default;

            try
            {
                var traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
                var spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
                var traceFlags = (ActivityTraceFlags)Convert.ToInt32(parts[3], 16);

                // Try to get tracestate if present
                string traceState = null;
                if (headers.TryGetValue(TraceStateHeaderName, out var traceStateObj))
                {
                    traceState = ConvertToString(traceStateObj);
                }

                return new ActivityContext(traceId, spanId, traceFlags, traceState);
            }
            catch
            {
                // If parsing fails, return default context
                return default;
            }
        }

        /// <summary>
        /// Extracts parent activity context from message headers (string dictionary variant).
        /// </summary>
        /// <param name="headers">Message headers dictionary.</param>
        /// <returns>ActivityContext if trace context is found, otherwise default.</returns>
        public static ActivityContext ExtractTraceContext(IDictionary<string, string> headers)
        {
            if (headers == null || !headers.Any())
                return default;

            // Try to get traceparent header
            if (!headers.TryGetValue(TraceParentHeaderName, out var traceParent))
                return default;

            if (string.IsNullOrWhiteSpace(traceParent))
                return default;

            // Parse W3C traceparent format: version-traceId-spanId-traceFlags
            // Example: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
            var parts = traceParent.Split('-');
            if (parts.Length != 4)
                return default;

            try
            {
                var traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
                var spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
                var traceFlags = (ActivityTraceFlags)Convert.ToInt32(parts[3], 16);

                string traceState = headers.TryGetValueOrDefault(TraceStateHeaderName);

                return new ActivityContext(traceId, spanId, traceFlags, traceState);
            }
            catch
            {
                // If parsing fails, return default context
                return default;
            }
        }

        /// <summary>
        /// Injects current activity context into message headers.
        /// </summary>
        /// <param name="headers">Message headers dictionary to inject into.</param>
        /// <param name="activity">The activity whose context to inject.</param>
        internal static void InjectTraceContext(IDictionary<string, object> headers, Activity activity)
        {
            if (headers == null || activity == null)
                return;

            // Create W3C traceparent: version-traceId-spanId-traceFlags
            var traceParent = $"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-{((int)activity.ActivityTraceFlags):x2}";
            headers[TraceParentHeaderName] = traceParent;

            // Add tracestate if present
            if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
            {
                headers[TraceStateHeaderName] = activity.TraceStateString;
            }
        }

        /// <summary>
        /// Converts header value to string, handling byte arrays and other types.
        /// </summary>
        private static string ConvertToString(object value)
        {
            if (value == null)
                return null;

            if (value is string str)
                return str;

            if (value is byte[] bytes)
                return Encoding.UTF8.GetString(bytes);

            return value.ToString();
        }
    }
}
