using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using MessageWorkerPool.Telemetry.Abstractions;

namespace MessageWorkerPool.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry implementation of IMetrics for WorkerPool operations.
    /// Tracks worker health, task processing, and performance metrics.
    /// </summary>
    public class OpenTelemetryMetrics : IMetrics, IDisposable
    {
        private readonly Meter _meter;
        private bool _disposed = false;

        // Counters
        private readonly Counter<long> _tasksProcessedCounter;
        private readonly Counter<long> _tasksFailedCounter;
        private readonly Counter<long> _tasksRejectedCounter;

        // Gauges (using ObservableGauge)
        private readonly ObservableGauge<int> _activeWorkersGauge;
        private readonly ObservableGauge<int> _processingTasksGauge;
        private readonly ObservableGauge<int> _healthyWorkersGauge;
        private readonly ObservableGauge<int> _stoppedWorkersGauge;

        // Histogram for task processing duration
        private readonly Histogram<double> _taskProcessingDuration;

        // State tracking
        private int _activeWorkers;
        private int _processingTasks;
        private int _healthyWorkers;
        private int _stoppedWorkers;

        /// <summary>
        /// Initializes a new instance of OpenTelemetryMetrics.
        /// </summary>
        /// <param name="meterName">The name of the meter (default: MessageWorkerPool).</param>
        /// <param name="version">The version of the instrumentation (default: 1.0.0).</param>
        public OpenTelemetryMetrics(string meterName = "MessageWorkerPool", string version = "1.0.0")
        {
            _meter = new Meter(meterName, version);

            // Initialize counters
            _tasksProcessedCounter = _meter.CreateCounter<long>(
                "worker_processed_tasks_total",
                description: "Total number of tasks successfully processed by workers");

            _tasksFailedCounter = _meter.CreateCounter<long>(
                "worker_failed_tasks_total",
                description: "Total number of tasks that failed during processing");

            _tasksRejectedCounter = _meter.CreateCounter<long>(
                "worker_rejected_tasks_total",
                description: "Total number of tasks rejected by workers");

            // Initialize observable gauges
            _activeWorkersGauge = _meter.CreateObservableGauge<int>(
                "workerpool_active_workers",
                () => _activeWorkers,
                description: "Number of active workers in the pool");

            _processingTasksGauge = _meter.CreateObservableGauge<int>(
                "workerpool_processing_tasks",
                () => _processingTasks,
                description: "Number of tasks currently being processed");

            _healthyWorkersGauge = _meter.CreateObservableGauge<int>(
                "workerpool_healthy_workers",
                () => _healthyWorkers,
                description: "Number of healthy (running) workers");

            _stoppedWorkersGauge = _meter.CreateObservableGauge<int>(
                "workerpool_stopped_workers",
                () => _stoppedWorkers,
                description: "Number of stopped workers");

            // Initialize histogram
            _taskProcessingDuration = _meter.CreateHistogram<double>(
                "worker_task_duration_seconds",
                unit: "s",
                description: "Duration of task processing in seconds");
        }

        /// <inheritdoc />
        public void RecordTaskProcessed(string queueName = null, string workerId = null)
        {
            _tasksProcessedCounter.Add(1, CreateTags(queueName, workerId));
        }

        /// <inheritdoc />
        public void RecordTaskFailed(string queueName = null, string workerId = null, string errorType = null)
        {
            var tags = CreateTags(queueName, workerId);
            if (!string.IsNullOrEmpty(errorType))
            {
                tags = new KeyValuePair<string, object>[]
                {
                    tags[0], tags[1],
                    new KeyValuePair<string, object>("error.type", errorType)
                };
            }
            _tasksFailedCounter.Add(1, tags);
        }

        /// <inheritdoc />
        public void RecordTaskRejected(string queueName = null, string workerId = null)
        {
            _tasksRejectedCounter.Add(1, CreateTags(queueName, workerId));
        }

        /// <inheritdoc />
        public void RecordTaskDuration(double durationMs, string queueName = null, string workerId = null)
        {
            // Convert milliseconds to seconds for Prometheus consistency
            _taskProcessingDuration.Record(durationMs / 1000.0, CreateTags(queueName, workerId));
        }

        /// <inheritdoc />
        public void SetActiveWorkers(int count)
        {
            _activeWorkers = count;
        }

        /// <inheritdoc />
        public void SetProcessingTasks(int count)
        {
            _processingTasks = count;
        }

        /// <inheritdoc />
        public void SetHealthyWorkers(int count)
        {
            _healthyWorkers = count;
        }

        /// <inheritdoc />
        public void SetStoppedWorkers(int count)
        {
            _stoppedWorkers = count;
        }

        /// <inheritdoc />
        public void IncrementProcessingTasks()
        {
            System.Threading.Interlocked.Increment(ref _processingTasks);
        }

        /// <inheritdoc />
        public void DecrementProcessingTasks()
        {
            System.Threading.Interlocked.Decrement(ref _processingTasks);
        }

        private static KeyValuePair<string, object>[] CreateTags(string queueName, string workerId)
        {
            return new[]
            {
                new KeyValuePair<string, object>("queue.name", queueName ?? "unknown"),
                new KeyValuePair<string, object>("worker.id", workerId ?? "unknown")
            };
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _meter?.Dispose();
            }

            _disposed = true;
        }
    }
}
