using System.Collections.Generic;
using MessagePack;
using MessagePack.Resolvers;

namespace MessageWorkerPool.Utilities
{
    /// <summary>
    /// Encapsulate message from MQ service
    /// </summary>
    [MessagePackObject]
    public class MessageInputTask
    {
        [Key(0)]
        public string Message { get;  set; }
        [Key(1)]
        public string CorrelationId { get;  set; }
        [Key(2)]
        public string OriginalQueueName { get;  set; }

        [Key(3)]
        [MessagePackFormatter(typeof(PrimitiveObjectResolver))]
        public IDictionary<string, object> Headers { get; set; }
    }
}
