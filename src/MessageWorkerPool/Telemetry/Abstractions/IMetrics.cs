using System.Collections.Generic;

namespace MessageWorkerPool.Telemetry.Abstractions
{
    /// <summary>
    /// Abstraction for recording metrics.
    /// </summary>
    public interface IMetrics
    {
        /// <summary>
        /// Records a successfully processed task.
        /// </summary>
        /// <param name="queueName">The queue name where the task came from.</param>
        /// <param name="workerId">The worker ID that processed the task.</param>
        void RecordTaskProcessed(string queueName = null, string workerId = null);

        /// <summary>
        /// Records a failed task.
        /// </summary>
        /// <param name="queueName">The queue name where the task came from.</param>
        /// <param name="workerId">The worker ID that processed the task.</param>
        /// <param name="errorType">The type of error that occurred.</param>
        void RecordTaskFailed(string queueName = null, string workerId = null, string errorType = null);

        /// <summary>
        /// Records a rejected task.
        /// </summary>
        /// <param name="queueName">The queue name where the task came from.</param>
        /// <param name="workerId">The worker ID that rejected the task.</param>
        void RecordTaskRejected(string queueName = null, string workerId = null);

        /// <summary>
        /// Records the duration of task processing.
        /// </summary>
        /// <param name="durationMs">Duration in milliseconds.</param>
        /// <param name="queueName">The queue name where the task came from.</param>
        /// <param name="workerId">The worker ID that processed the task.</param>
        void RecordTaskDuration(double durationMs, string queueName = null, string workerId = null);

        /// <summary>
        /// Updates the number of active workers.
        /// </summary>
        void SetActiveWorkers(int count);

        /// <summary>
        /// Updates the number of tasks currently being processed.
        /// </summary>
        void SetProcessingTasks(int count);

        /// <summary>
        /// Updates the number of healthy workers.
        /// </summary>
        void SetHealthyWorkers(int count);

        /// <summary>
        /// Updates the number of stopped workers.
        /// </summary>
        void SetStoppedWorkers(int count);

        /// <summary>
        /// Increments the number of processing tasks.
        /// </summary>
        void IncrementProcessingTasks();

        /// <summary>
        /// Decrements the number of processing tasks.
        /// </summary>
        void DecrementProcessingTasks();
    }
}