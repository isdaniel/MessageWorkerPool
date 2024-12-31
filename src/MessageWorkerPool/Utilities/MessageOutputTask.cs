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
    }
}
