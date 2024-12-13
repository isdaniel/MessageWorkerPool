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

        protected string Group { get; }
        protected string Message { get; }
        protected string CorrelationId { get; }
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
