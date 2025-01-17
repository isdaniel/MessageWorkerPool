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
        /// <summary>
        /// Task body
        /// </summary>
        [Key("0")]
        public string Message { get;  set; }
        /// <summary>
        /// Message CorrelationId for debugging issue between, producer and consumer
        /// </summary>
        [Key("1")]
        public string CorrelationId { get;  set; }
        /// <summary>
        /// Original sending Queue Name
        /// </summary>
        [Key("2")]
        public string OriginalQueueName { get;  set; }
        /// <summary>
        /// TimeoutMilliseconds : The time span to wait before canceling this (milliseconds),
        /// default: -1, if value smaller than 0 represent InfiniteTimeSpan, otherwise use the setting positive value.
        /// </summary>
        [Key("3")]
        [MessagePackFormatter(typeof(PrimitiveObjectResolver))]
        public IDictionary<string, object> Headers { get; set; }
    }
}
