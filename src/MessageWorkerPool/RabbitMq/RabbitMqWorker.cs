using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MessageWorkerPool.Utilities;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Channels;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using MessageWorkerPool.Extensions;

/// <summary>
/// Represents a worker that processes messages from a RabbitMQ queue.
/// The worker communicates with an external process through standard input/output and handles message acknowledgment or rejection based on processing outcomes.
/// </summary>
namespace MessageWorkerPool.RabbitMq
{
    /// <summary>
    /// worker base to handle infrastructure and connection matter, export an Execute method let subclass implement their logic
    /// </summary>
    public class RabbitMqWorker : WorkerBase 
    {
        public RabbitMqSetting Setting { get; }
        protected AsyncEventHandler<BasicDeliverEventArgs> ReceiveEvent;
        private AsyncEventingBasicConsumer _consumer;
        int _messageCount = 0;
        internal IModel channel { get; private set; }

        internal ConcurrentBag<ulong> RejectMessageDeliveryTags { get; private set; } = new ConcurrentBag<ulong>();

        protected ILogger<RabbitMqWorker> Logger { get; }

        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitMqWorker"/> class.
        /// </summary>
        /// <param name="setting">RabbitMQ settings.</param>
        /// <param name="workerSetting">Worker pool settings.</param>
        /// <param name="channel">RabbitMQ channel.</param>
        /// <param name="loggerFactory">Logger factory instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required parameter is null.</exception>
        public RabbitMqWorker(
            RabbitMqSetting setting,
            WorkerPoolSetting workerSetting,
            IModel channel,
            ILogger<RabbitMqWorker> logger) : base(workerSetting, logger)
        {
            if (workerSetting == null)
                throw new ArgumentNullException(nameof(workerSetting));

            if (setting == null)
                throw new ArgumentNullException(nameof(setting));

            Setting = setting;
            this.channel = channel;
            this.Logger = logger;
        }

        /// <summary>
        /// PipeStreamWrapper.WriteAsync: sending in message that get from MQ to worker .
        /// PipeStreamWrapper: MESSAGE_DONE or MESSAGE_DONE_WITH_REPLY = Finish task, we can do BasicAck, otherwise will wait for signal that we can ack.
        /// </summary>
        /// <param name="e">Delivery event arguments containing the message details.</param>
		/// <param name="correlationId">Correlation ID of the message.</param>
		/// <param name="token">Cancellation token.</param>
        private async Task ProcessingMessage(BasicDeliverEventArgs e, string correlationId, CancellationToken token)
        {
            
            try
            {
                var message = Encoding.UTF8.GetString(e.Body.Span.ToArray());
                Logger.LogDebug($"received message:{message}");
                var task = new MessageInputTask
                {
                    Message = message,
                    CorrelationId = correlationId,
                    Headers = e.BasicProperties.Headers.ConvertToStringMap(),
                    OriginalQueueName = _workerSetting.QueueName,
                };
                await DataStreamWriteAsync(task);

                var taskOutput = await ReadAndProcessOutputAsync(token);

                if (_messageDoneMap.Contains(taskOutput.Status))
                {
                    AcknowledgeMessage(e.DeliveryTag);
                    string replyQueue = !string.IsNullOrWhiteSpace(taskOutput.ReplyQueueName) ? taskOutput.ReplyQueueName : e.BasicProperties.ReplyTo;

                    ReplyQueue(replyQueue, taskOutput, () => {
                        var properties = e.BasicProperties;
                        properties.ContentEncoding = Encoding.UTF8.WebName;
                        properties.Headers = taskOutput.Headers.ConvertToObjectMap();

                        //TODO! We could support let user fill queue or exchange name from worker protocol in future.
                        channel.BasicPublish(string.Empty, replyQueue, properties, Encoding.UTF8.GetBytes(taskOutput.Message));
                    });
                }
                else
                {
                    RejectMessage(e.DeliveryTag);
                }
            }
            catch (Exception ex)
            {
                RejectMessage(e.DeliveryTag);
                Logger.LogWarning(ex, "Processing message encountered an exception!");
            }
        }

        /// <summary>
        /// Reads and processes the output from the external process, handling task completion statuses.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The parsed output task from the external process.</returns>
        private async Task<MessageOutputTask> ReadAndProcessOutputAsync(CancellationToken token)
        {
            var taskOutput = new MessageOutputTask()
            {
                Status = MessageStatus.IGNORE_MESSAGE
            };

            while (!token.IsCancellationRequested)
            {
                try
                {
                    taskOutput = await DataStreamReadAsync<MessageOutputTask>();
                }
                catch (JsonException ex)
                {
                    Logger.LogError(ex, "Error parsing MessageOutputTask JSON, it might lead worker in infinite loop!");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Unexpected error during JSON parsing.");
                    throw;
                }

                if (_messageDoneMap.Contains(taskOutput.Status))
                {
                    break;
                }
            }

            return taskOutput;
        }

        private void AcknowledgeMessage(ulong deliveryTag)
        {
            channel.BasicAck(deliveryTag, false);
            Logger.LogDebug($"Channel ChannelNumber {channel.ChannelNumber},Message {deliveryTag} acknowledged.");
        }

        private void RejectMessage(ulong deliveryTag)
        {
            channel.BasicNack(deliveryTag, false, true);
            Logger.LogDebug($"Channel ChannelNumber {channel.ChannelNumber},Message {deliveryTag} rejected.");
        }

        /// <summary>
        /// provide hock for sub-class implement
        /// </summary>
        /// <returns></returns>
        protected override async Task GracefulReleaseAsync(CancellationToken token)
        {
            while (Interlocked.CompareExchange(ref _messageCount, 0, 0) != 0)
            {
                Logger.LogInformation($"Waiting for all messages to be processed. Current messageCount: {_messageCount}");
                _receivedWaitEvent.WaitOne();
            }

            //reject all messages from this Channel.
            RejectRemainingMessages();

            if (ReceiveEvent != null)
            {
                _consumer.Received -= ReceiveEvent;
                ReceiveEvent = null;
            }

            Logger.LogInformation("RabbitMQ Conn Closed!!!!");
        }
        

        private void RejectRemainingMessages()
        {
            Logger.LogInformation("Rejecting all remaining messages in the queue...");
            Logger.LogInformation($"messages {RejectMessageDeliveryTags.Count} are waiting for rejecting from the queue.");
            foreach (var deliveryTag in RejectMessageDeliveryTags)
            {
                RejectMessage(deliveryTag);
            }
            Logger.LogInformation("Rejected all remaining messages in the queue...");
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        /// <param name="disposing">Indicates whether to release managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (channel != null)
                {
                    try
                    {
                        if (!channel.IsClosed)
                        {
                            channel.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Error while closing RabbitMQ channel.");
                    }
                    finally
                    {
                        channel.Dispose();
                        channel = null;
                    }
                }
            }

            _disposed = true;
            base.Dispose(disposing);
        }

        protected override void SetupMessageQueueSetting(CancellationToken token)
        {
            _consumer = new AsyncEventingBasicConsumer(channel);
            ReceiveEvent = async (sender, e) =>
            {
                var correlationId = e.BasicProperties.CorrelationId;

                using (Logger.BeginScope($"[Pid: {Process.Id}][CorrelationId: {correlationId}]"))
                {
                    if (_stoppingStatus.Contains(this.Status) || token.IsCancellationRequested)
                    {
                        Logger.LogWarning($"doing GracefulShutDown reject message!");
                        //it should return, if the worker are processing GracefulShutDown.
                        RejectMessageDeliveryTags.Add(e.DeliveryTag);
                        return;
                    }
                    Interlocked.Increment(ref _messageCount);
                    await ProcessingMessage(e, correlationId, token).ConfigureAwait(false);
                    Interlocked.Decrement(ref _messageCount);
                    _receivedWaitEvent.Set();
                }
            };
            _consumer.Received += ReceiveEvent;
            channel.BasicQos(0, Setting.PrefetchTaskCount, false);
            channel.BasicConsume(_workerSetting.QueueName, false, _consumer);
            Logger.LogInformation($"Starting.. Channel ChannelNumber {channel.ChannelNumber}");
        }
    }
}
