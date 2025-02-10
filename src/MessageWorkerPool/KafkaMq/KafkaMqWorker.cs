using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.Utilities;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool.KafkaMq
{
    /// <summary>
    /// KafkaMqWorker is responsible for consuming messages from a Kafka topic and processing them.
    /// It also handles message replies when required.
    /// </summary>
    /// <typeparam name="TKey">The type of the key for Kafka messages.</typeparam>
    public class KafkaMqWorker<TKey> : WorkerBase
    {
        private readonly KafkaSetting<TKey> _kafkaSetting; // Configuration settings for Kafka.
        private bool _disposed = false; // Tracks whether the object has been disposed.
        private IConsumer<TKey, string> _consumer; // Kafka consumer for receiving messages.
        private IProducer<TKey, string> _producer; // Kafka producer for sending messages.
        private Task _consumptionTask; // Task responsible for running the message consumption loop.
        private readonly CancellationTokenSource _cancellationTokenSource; // Token source for handling graceful shutdown.

        /// <summary>
        /// Initializes a new instance of KafkaMqWorker.
        /// </summary>
        /// <param name="workerSetting">Settings for worker pooling.</param>
        /// <param name="kafkaSetting">Kafka configuration settings.</param>
        /// <param name="logger">Logger instance for logging messages.</param>
        public KafkaMqWorker(WorkerPoolSetting workerSetting, KafkaSetting<TKey> kafkaSetting, ILogger logger)
            : base(workerSetting, logger)
        {
            _kafkaSetting = kafkaSetting;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Sets up the Kafka message queue by initializing the consumer and producer,
        /// then starts the message consumption loop.
        /// </summary>
        /// <param name="token">Cancellation token to handle graceful shutdown.</param>
        protected override void SetupMessageQueueSetting(CancellationToken token)
        {
            _consumer = _kafkaSetting.GetConsumer(); // Initialize Kafka consumer.
            _producer = _kafkaSetting.GetProducer(); // Initialize Kafka producer.
            _consumer.Subscribe(_workerSetting.QueueName); // Subscribe to the Kafka topic.

            // Create a linked cancellation token to handle graceful shutdown.
            var taskToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, token).Token;

            // Start the message consumption loop in a separate task.
            _consumptionTask = Task.Run(async () => await StartMessageConsumptionLoop(taskToken));
        }

        /// <summary>
        /// Continuously consumes messages from Kafka and processes them asynchronously.
        /// </summary>
        /// <param name="token">Cancellation token to stop the loop gracefully.</param>
        private async Task StartMessageConsumptionLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !_stoppingStatus.Contains(this.Status))
            {
                try
                {
                    // Consume a message from Kafka.
                    var result = _consumer.Consume(token);
                    await HandleMessageAsync(result).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break; // Exit loop on cancellation.
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Unexpected error in message loop"); // Log unexpected errors.
                }
            }
        }

        /// <summary>
        /// Handles a consumed message by processing it and handling responses if needed.
        /// </summary>
        /// <param name="result">The consumed Kafka message.</param>
        private async Task HandleMessageAsync(ConsumeResult<TKey, string> result)
        {
            // Extract message headers.
            var headers = result.Message.Headers.ToDictionary(x => x.Key, x => Encoding.UTF8.GetString(x.GetValueBytes()));

            Logger.LogDebug($"received message:{result.Message.Value}");

            // Write the received message into the data stream.
            await DataStreamWriteAsync(new MessageInputTask
            {
                Message = result.Message.Value,
                CorrelationId = headers.TryGetValueOrDefault("CorrelationId"),
                Headers = headers,
                OriginalQueueName = _workerSetting.QueueName,
            });

            // Read processed data from the stream.
            var taskOutput = await DataStreamReadAsync<MessageOutputTask>();

            // If the message processing is completed, handle the successful message.
            if (_messageDoneMap.Contains(taskOutput.Status))
            {
                await HandleSuccessfulMessage(result, headers, taskOutput);
            }
        }

        /// <summary>
        /// Handles a successfully processed message by sending a reply if necessary.
        /// </summary>
        /// <param name="result">The original consumed Kafka message.</param>
        /// <param name="headers">Extracted headers from the message.</param>
        /// <param name="taskOutput">The processed message output.</param>
        private async Task HandleSuccessfulMessage(ConsumeResult<TKey, string> result, Dictionary<string, string> headers, MessageOutputTask taskOutput)
        {
            // Determine the reply queue from the message headers or output.
            string replyQueue = !string.IsNullOrWhiteSpace(taskOutput.ReplyQueueName) ?
                                taskOutput.ReplyQueueName :
                                headers.TryGetValueOrDefault("ReplyTo");

            // Send the response message to the determined reply queue.
            await ReplyQueueAsync(replyQueue, taskOutput, (Func<Task>)(async () =>
            {
                var replyHeaders = new Headers();
                foreach (var item in headers)
                {
                    replyHeaders.Add(item.Key, Encoding.UTF8.GetBytes(item.Value));
                }

                await _producer.ProduceAsync(replyQueue, new Message<TKey, string>()
                {
                    Headers = replyHeaders,
                    Value = taskOutput.Message
                });
            }));

            // Commit the offset to mark the message as processed.
            _consumer.Commit(result);
        }

        /// <summary>
        /// Handles graceful shutdown by canceling the message consumption loop.
        /// </summary>
        /// <param name="token">Cancellation token to wait for cleanup.</param>
        protected override async Task GracefulReleaseAsync(CancellationToken token)
        {
            _cancellationTokenSource.Cancel(); // Cancel the consumption task.

            if (_consumptionTask != null)
            {
                await _consumptionTask; // Wait for the task to complete.
            }

            await base.GracefulReleaseAsync(token);
        }

        /// <summary>
        /// Disposes resources such as Kafka consumer and producer.
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
                if (_consumer != null)
                {
                    _consumer.Close();
                    _consumer.Dispose();
                    _consumer = null;
                }

                if (_producer != null)
                {
                    _producer.Flush();
                    _producer.Dispose();
                    _producer = null;
                }
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }

}
