using System.Collections.Generic;

namespace MessageWorkerPool.Utilities
{
    /// <summary>
    /// Encapsulate message from MQ service
    /// </summary>
    public class MessageOutputTask
    {
        /// <summary>
        /// Output message from process
        /// </summary>
        public string Message { get; set; }
        public MessageStatus Status { get; set; }

        /// <summary>
        /// Reply information that we want to store for continue execution message.
        /// </summary>
        public IDictionary<string, object> Headers { get; set; }

        /// <summary>
        /// Default use BasicProperties.Reply To queue name, task processor can overwrite reply queue name.
        /// </summary>
        /// <value>Default use BasicProperties.Reply</value>
        public string ReplyQueueName { get; set; }
    }
}
