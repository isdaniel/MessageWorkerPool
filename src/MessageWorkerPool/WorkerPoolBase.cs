using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.Telemetry.Abstractions;
using System.Linq;
using MessageWorkerPool.Utilities;

namespace MessageWorkerPool
{

    /// <summary>
	/// Represents the base implementation for a worker pool.
	/// Manages a collection of workers that process messages concurrently.
	/// This abstract class serves as a foundation for specific implementations, such as RabbitMQ or Kafka workers.
	/// </summary>
    public abstract class WorkerPoolBase : IWorkerPool
    {
		/// <summary>
		/// The number of worker processes to be created in the pool.
		/// </summary>
        public ushort ProcessCount { get; }

        protected readonly WorkerPoolSetting _workerSetting;
        protected readonly ILogger<WorkerPoolBase> _logger;
        protected readonly ILoggerFactory _loggerFactory;
        protected readonly ITelemetryManager _telemetryManager;
        private bool _isClosed = false;

		/// <summary>
		/// Indicates whether the worker pool is closed and no longer operational.
		/// </summary>
        public bool IsClosed  => _isClosed;
		/// <summary>
		/// The list of worker instances managed by the pool.
		/// </summary>
        protected readonly List<IWorker> Workers = new List<IWorker>();
        private bool _disposed = false;

        /// <summary>
        /// Timer for periodic health status updates.
        /// </summary>
        private Timer _healthCheckTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerPoolBase"/> class.
        /// </summary>
        /// <param name="workerSetting">The configuration settings for the worker pool.</param>
        /// <param name="loggerFactory">The logger factory used to create loggers for the pool and workers.</param>
        /// <param name="telemetryManager">The telemetry manager for tracking pool operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="workerSetting"/> is null.</exception>
        public WorkerPoolBase(
            WorkerPoolSetting workerSetting,
            ILoggerFactory loggerFactory,
            ITelemetryManager telemetryManager = null)
        {
            if (workerSetting == null)
            {
                throw new ArgumentNullException(nameof(workerSetting));
            }

            _loggerFactory = loggerFactory;
            this._logger = _loggerFactory.CreateLogger<WorkerPoolBase>();
            _workerSetting = workerSetting;
            _telemetryManager = telemetryManager ?? new TelemetryManager(NoOpTelemetryProvider.Instance);
            ProcessCount = workerSetting.WorkerUnitCount;

            // Initialize telemetry
            _telemetryManager.Metrics?.SetActiveWorkers(0);

            // Start periodic health check updates (every 5 seconds)
            _healthCheckTimer = new Timer(_ => UpdateHealthMetrics(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }


		/// <summary>
		/// Creates a new worker instance. This method must be implemented by derived classes
		/// to provide specific worker creation logic, such as RabbitMQ or Kafka workers.
		/// </summary>
		/// <returns>An instance of <see cref="IWorker"/>.</returns>
        protected abstract IWorker GetWorker();


		/// <summary>
		/// Initializes the worker pool by creating and initializing the configured number of workers.
		/// </summary>
		/// <param name="token">A token to monitor for cancellation requests.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
        public async Task InitPoolAsync(CancellationToken token)
        {
            using (var activity = _telemetryManager.StartPoolInitActivity(ProcessCount, _workerSetting.QueueName))
            {
                try
                {
                    for (int i = 0; i < ProcessCount; i++)
                    {
                        IWorker worker = GetWorker();

                        await worker.InitWorkerAsync(token);
                        Workers.Add(worker);
                    }

                    _telemetryManager.Metrics?.SetActiveWorkers(Workers.Count);
                    UpdateHealthMetrics();

                    _logger.LogInformation($"Worker pool initialized with {Workers.Count} workers for queue '{_workerSetting.QueueName}'");
                }
                catch (Exception ex)
                {
                    _telemetryManager.RecordException(activity, ex);
                    _logger.LogError(ex, "Failed to initialize worker pool");
                    throw;
                }
            }
        }


		/// <summary>
		/// Waits for all workers to finish their processing and performs a graceful shutdown.
		/// Releases resources and marks the pool as closed.
		/// </summary>
		/// <param name="token">A token to monitor for cancellation requests.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
        public async Task WaitFinishedAsync(CancellationToken token)
        {
            foreach (var worker in Workers)
            {
                await worker.GracefulShutDownAsync(token);
            }

            Dispose();
        }

        /// <summary>
        /// Updates health metrics based on current worker statuses.
        /// </summary>
        private void UpdateHealthMetrics()
        {
            try
            {
                var summary = WorkerPoolInformationCollector.CollectWorkerStatus(Workers, _workerSetting.QueueName);

                _telemetryManager.Metrics?.SetHealthyWorkers(summary.HealthyWorkers);
                _telemetryManager.Metrics?.SetStoppedWorkers(summary.StoppedWorkers);
                _telemetryManager.Metrics?.SetActiveWorkers(summary.TotalWorkers);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update health metrics");
            }
        }

        /// <summary>
        /// Gets detailed information about the worker pool and its workers.
        /// </summary>
        /// <returns>WorkerPoolInformation object containing pool and worker details.</returns>
        public virtual WorkerPoolInformation GetPoolInformation()
        {
            var summary = WorkerPoolInformationCollector.CollectWorkerStatus(Workers, _workerSetting.QueueName);

            return new WorkerPoolInformation
            {
                QueueName = _workerSetting.QueueName,
                TotalWorkers = summary.TotalWorkers,
                HealthyWorkers = summary.HealthyWorkers,
                StoppedWorkers = summary.StoppedWorkers,
                StoppingWorkers = summary.StoppingWorkers,
                WaitingWorkers = summary.WaitingWorkers,
                CommandLine = _workerSetting.CommandLine,
                IsClosed = _isClosed,
                Workers = Workers.OfType<WorkerBase>().Select(w => new WorkerInformation
                {
                    WorkerId = w.WorkerId,
                    ProcessId = w.ProcessId ?? 0,
                    Status = w.Status,
                    QueueName = _workerSetting.QueueName
                }).ToList()
            };
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

		/// <summary>
		/// Releases the unmanaged resources used by the worker pool and optionally releases the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _healthCheckTimer?.Dispose();
                _healthCheckTimer = null;

                foreach (var worker in Workers)
                {
                    worker.Dispose();
                }
            }

            _disposed = true;
            _isClosed = true;
        }
    }
}
