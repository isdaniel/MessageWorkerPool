using System.Collections.Generic;
using MessagePack;
using MessagePack.Resolvers;

namespace MessageWorkerPool.Utilities
{
    /// <summary>
    /// Encapsulate message from MQ service
    /// </summary>
    [MessagePackObject]
    public class MessageOutputTask
    {
        /// <summary>
        /// Output message from process
        /// </summary>
        [Key(0)]
        public string Message { get; set; }
        [Key(1)]
        public MessageStatus Status { get; set; }
        /// <summary>
        /// Reply information that we want to store for continue execution message.
        /// </summary>
        [Key(2)]
        [MessagePackFormatter(typeof(PrimitiveObjectResolver))]
        public IDictionary<string, object> Headers { get; set; }
        /// <summary>
        /// Default use BasicProperties.Reply To queue name, task processor can overwrite reply queue name.
        /// </summary>
        /// <value>Default use BasicProperties.Reply</value>
        [Key(3)]
        public string ReplyQueueName { get; set; }
    }
}
