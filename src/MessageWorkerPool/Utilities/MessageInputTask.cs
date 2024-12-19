using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool.Utilities
{
    /// <summary>
    /// Encapsulate message from MQ service
    /// </summary>
    public class MessageInputTask
    {
        public string Message { get;  set; }
        public string CorrelationId { get;  set; }

        internal string ToJsonMessage()
        {
            return JsonSerializer.Serialize(new { Message, CorrelationId });
        }
    }


    /// <summary>
    /// Encapsulate message from MQ service
    /// </summary>
    public class MessageOutputTask
    {
        public string Message { get; set; }
        public MessageStatus Stauts { get; set; } = MessageStatus.IGNORE_MESSAGE;
    }
}
