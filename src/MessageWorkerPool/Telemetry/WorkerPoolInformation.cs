using System;
using System.Collections.Generic;
using System.Linq;
using MessageWorkerPool.Utilities;

namespace MessageWorkerPool.Telemetry
{
    /// <summary>
    /// Collects and provides basic information about workers and worker pools.
    /// </summary>
    public class WorkerPoolInformation
    {
        /// <summary>
        /// Gets or sets the worker pool ID.
        /// </summary>
        public string PoolId { get; set; }

        /// <summary>
        /// Gets or sets the queue name being processed.
        /// </summary>
        public string QueueName { get; set; }

        /// <summary>
        /// Gets or sets the total number of workers configured.
        /// </summary>
        public int TotalWorkers { get; set; }

        /// <summary>
        /// Gets or sets the number of healthy (running) workers.
        /// </summary>
        public int HealthyWorkers { get; set; }

        /// <summary>
        /// Gets or sets the number of stopped workers.
        /// </summary>
        public int StoppedWorkers { get; set; }

        /// <summary>
        /// Gets or sets the number of stopping workers.
        /// </summary>
        public int StoppingWorkers { get; set; }

        /// <summary>
        /// Gets or sets the number of workers waiting for initialization.
        /// </summary>
        public int WaitingWorkers { get; set; }

        /// <summary>
        /// Gets or sets the command line used to start workers.
        /// </summary>
        public string CommandLine { get; set; }

        /// <summary>
        /// Gets or sets the creation time of the pool.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets whether the pool is closed.
        /// </summary>
        public bool IsClosed { get; set; }

        /// <summary>
        /// Gets detailed information about individual workers.
        /// </summary>
        public List<WorkerInformation> Workers { get; set; } = new List<WorkerInformation>();

        /// <summary>
        /// Calculates worker health percentage.
        /// </summary>
        public double HealthPercentage
        {
            get
            {
                if (TotalWorkers == 0) return 0;
                return (double)HealthyWorkers / TotalWorkers * 100;
            }
        }
    }

    /// <summary>
    /// Contains detailed information about an individual worker.
    /// </summary>
    public class WorkerInformation
    {
        /// <summary>
        /// Gets or sets the worker ID (usually process ID).
        /// </summary>
        public string WorkerId { get; set; }

        /// <summary>
        /// Gets or sets the process ID.
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the current status of the worker.
        /// </summary>
        public WorkerStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the queue name this worker is consuming from.
        /// </summary>
        public string QueueName { get; set; }

        /// <summary>
        /// Gets or sets the total number of tasks processed by this worker.
        /// </summary>
        public long TasksProcessed { get; set; }

        /// <summary>
        /// Gets or sets the number of tasks currently being processed.
        /// </summary>
        public int CurrentTaskCount { get; set; }

        /// <summary>
        /// Gets or sets the creation time of the worker.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the last activity time of the worker.
        /// </summary>
        public DateTime LastActivityAt { get; set; }

        /// <summary>
        /// Determines if the worker is healthy (running status).
        /// </summary>
        public bool IsHealthy => Status == WorkerStatus.Running;
    }

    /// <summary>
    /// Helper class to collect worker pool information from a WorkerPoolBase instance.
    /// </summary>
    public static class WorkerPoolInformationCollector
    {
        /// <summary>
        /// Collects basic information from workers for telemetry purposes.
        /// </summary>
        /// <param name="workers">The list of workers to collect information from.</param>
        /// <param name="queueName">The queue name being processed.</param>
        /// <returns>A summary of worker statuses.</returns>
        public static WorkerStatusSummary CollectWorkerStatus(IEnumerable<IWorker> workers, string queueName)
        {
            var summary = new WorkerStatusSummary
            {
                QueueName = queueName,
                TotalWorkers = workers.Count()
            };

            foreach (var worker in workers)
            {
                if (worker is WorkerBase workerBase)
                {
                    switch (workerBase.Status)
                    {
                        case WorkerStatus.Running:
                            summary.HealthyWorkers++;
                            break;
                        case WorkerStatus.Stopped:
                            summary.StoppedWorkers++;
                            break;
                        case WorkerStatus.Stopping:
                            summary.StoppingWorkers++;
                            break;
                        case WorkerStatus.WaitForInit:
                            summary.WaitingWorkers++;
                            break;
                    }
                }
            }

            return summary;
        }
    }

    /// <summary>
    /// Summary of worker statuses in a pool.
    /// </summary>
    public class WorkerStatusSummary
    {
        public string QueueName { get; set; }
        public int TotalWorkers { get; set; }
        public int HealthyWorkers { get; set; }
        public int StoppedWorkers { get; set; }
        public int StoppingWorkers { get; set; }
        public int WaitingWorkers { get; set; }
    }
}
