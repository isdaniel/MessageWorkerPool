using System;
using System.Collections.Generic;

namespace MessageWorkerPool.Telemetry.Abstractions
{
    /// <summary>
    /// Represents an activity/span for distributed tracing.
    /// </summary>
    public interface IActivity : IDisposable
    {
        /// <summary>
        /// Adds a tag to the activity.
        /// </summary>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        void SetTag(string key, object value);

        /// <summary>
        /// Adds multiple tags to the activity.
        /// </summary>
        /// <param name="tags">The tags to add.</param>
        void SetTags(IDictionary<string, object> tags);

        /// <summary>
        /// Sets the activity status.
        /// </summary>
        /// <param name="status">The status of the activity.</param>
        /// <param name="description">Optional description of the status.</param>
        void SetStatus(ActivityStatus status, string description = null);

        /// <summary>
        /// Records an exception that occurred during the activity.
        /// </summary>
        /// <param name="exception">The exception to record.</param>
        void RecordException(Exception exception);
    }

    /// <summary>
    /// Status of an activity.
    /// </summary>
    public enum ActivityStatus
    {
        /// <summary>
        /// The activity completed successfully.
        /// </summary>
        Ok,

        /// <summary>
        /// The activity failed with an error.
        /// </summary>
        Error
    }
}