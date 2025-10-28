using System;
using System.Collections.Generic;
using System.Diagnostics;
using MessageWorkerPool.Telemetry.Abstractions;

namespace MessageWorkerPool.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry implementation of IActivity.
    /// </summary>
    public class OpenTelemetryActivity : IActivity
    {
        private readonly Activity _activity;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of OpenTelemetryActivity.
        /// </summary>
        /// <param name="activity">The underlying OpenTelemetry Activity.</param>
        public OpenTelemetryActivity(Activity activity)
        {
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        }

        /// <inheritdoc />
        public void SetTag(string key, object value)
        {
            if (_disposed || string.IsNullOrEmpty(key))
                return;

            _activity.SetTag(key, value?.ToString());
        }

        /// <inheritdoc />
        public void SetTags(IDictionary<string, object> tags)
        {
            if (_disposed || tags == null)
                return;

            foreach (var tag in tags)
            {
                SetTag(tag.Key, tag.Value);
            }
        }

        /// <inheritdoc />
        public void SetStatus(ActivityStatus status, string description = null)
        {
            if (_disposed)
                return;

            var otelStatus = status == ActivityStatus.Ok ? ActivityStatusCode.Ok :
                           status == ActivityStatus.Error ? ActivityStatusCode.Error :
                           ActivityStatusCode.Unset;

            _activity.SetStatus(otelStatus, description);
        }

        /// <inheritdoc />
        public void RecordException(Exception exception)
        {
            if (_disposed || exception == null)
                return;

            SetTag("exception.type", exception.GetType().FullName);
            SetTag("exception.message", exception.Message);
            SetTag("exception.stacktrace", exception.StackTrace);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed)
            {
                _activity?.Dispose();
                _disposed = true;
            }
        }
    }
}
