using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MessageWorkerPool.OpenTelemetry
{
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
        /// Injects current activity context into message headers.
        /// </summary>
        /// <param name="headers">Message headers dictionary to inject into.</param>
        /// <param name="activity">The activity whose context to inject.</param>
        public static void InjectTraceContext(IDictionary<string, object> headers, Activity activity)
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
