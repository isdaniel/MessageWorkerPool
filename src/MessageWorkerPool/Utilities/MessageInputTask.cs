using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool.Utilities
{
    /// <summary>
    /// Encapsulate message from MQ service
    /// </summary>
    public class MessageInputTask
    {
        public IDictionary<string,object> Headers { get; set; }
        public string Message { get;  set; }
        public string CorrelationId { get;  set; }

        internal string ToJsonMessage()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
