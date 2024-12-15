using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool
{
    /// <summary>
    /// encapsulate message from MQ service
    /// </summary>
    public class MessageTask
    {
        private readonly ILogger _logger;

        public string Group { get; private set; }
        public string Message { get; private set; }
        public string CorrelationId { get; private set; }
        public MessageTask(string message, string group, string correlationId, ILogger logger)
        {
            Message = message;
            Group = group;
            CorrelationId = correlationId;
            this._logger = logger;
        }

        internal string ToJsonMessage()
        {
            return JsonSerializer.Serialize(new { this.Group, this.Message, this.CorrelationId });
        }
    }
}
