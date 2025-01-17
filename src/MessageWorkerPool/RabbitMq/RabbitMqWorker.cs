using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MessageWorkerPool.Utilities;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.IO.Pipes;
using System.IO;
using MessageWorkerPool.IO;
using System.Linq;
using MessagePack;

/// <summary>
/// Represents a worker that processes messages from a RabbitMQ queue.
/// The worker communicates with an external process through standard input/output and handles message acknowledgment or rejection based on processing outcomes.
/// </summary>
namespace MessageWorkerPool.RabbitMq
{

    /// <summary>
    /// worker base to handle infrastructure and connection matter, export an Execute method let subclass implement their logic
    /// </summary>
    public class RabbitMqWorker : IWorker
    {
        public RabbitMqSetting Setting { get; }
        protected AsyncEventHandler<BasicDeliverEventArgs> ReceiveEvent;
        private AsyncEventingBasicConsumer _consumer;
        int _messageCount = 0;
        internal IModel channel { get; private set; }
        protected IProcessWrapper Process { get; private set; }
        private readonly WorkerPoolSetting _workerSetting;
        private readonly ILoggerFactory _loggerFactory;
        private readonly HashSet<WorkerStatus> _stoppingStatus = new HashSet<WorkerStatus>(){
            WorkerStatus.Stopped,
            WorkerStatus.Stopping
        };

        //Message Finish Statuss
        private readonly HashSet<MessageStatus> _messageDoneMap = new HashSet<MessageStatus>(){
            MessageStatus.MESSAGE_DONE,
            MessageStatus.MESSAGE_DONE_WITH_REPLY
        };

        internal ConcurrentBag<ulong> RejectMessageDeliveryTags { get; private set; } = new ConcurrentBag<ulong>();

        protected AutoResetEvent _receivedWaitEvent = new AutoResetEvent(false);

        /// <summary>
        /// Worker status
        /// </summary>
        private volatile WorkerStatus _status = WorkerStatus.WaitForInit;
        public WorkerStatus Status
        {
            get { return _status; }
        }
        protected ILogger<RabbitMqWorker> Logger { get; }

        private bool _disposed = false;

        private PipeStreamWrapper _pipeDataStream;

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
            ILoggerFactory loggerFactory)
        {
            if (workerSetting == null)
                throw new ArgumentNullException(nameof(workerSetting));

            if (setting == null)
                throw new ArgumentNullException(nameof(setting));

            _loggerFactory = loggerFactory;
            Logger = _loggerFactory.CreateLogger<RabbitMqWorker>();
            Setting = setting;
            _workerSetting = workerSetting;
            this.channel = channel;
        }

        protected virtual IProcessWrapper CreateProcess(ProcessStartInfo processStartInfo)
        {

            IProcessWrapper process = new ProcessWrapper(new Process
            {
                StartInfo = processStartInfo
            });

            return process;
        }

        /// <summary>
        /// Initializes the worker, setting up the external process and RabbitMQ consumer.
        /// </summary>
        /// <param name="token">Cancellation token for stopping the initialization.</param>
        public virtual async Task InitWorkerAsync(CancellationToken token)
        {
            _consumer = new AsyncEventingBasicConsumer(channel);
            Process = CreateProcess(new ProcessStartInfo()
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                FileName = _workerSetting.CommandLine,
                Arguments = _workerSetting.Arguments,
                CreateNoWindow = true,
                StandardErrorEncoding = Encoding.UTF8
            });
            StartProcess();

            using (Logger.BeginScope($"[Pid: {Process.Id}]"))
            {

                Logger.LogInformation($"Setup Process!");
                string pipeName = $"pipeDataStream_{Guid.NewGuid().ToString("N")}";
                await Process.StandardInput.WriteLineAsync(pipeName).ConfigureAwait(false);
                _pipeDataStream = await CreateOperationPipeAsync(pipeName).ConfigureAwait(false);
                Logger.LogInformation($"data pipe create successfully: {pipeName}");
                ReceiveEvent = async (sender, e) =>
                {
                    var correlationId = e.BasicProperties.CorrelationId;

                    using (Logger.BeginScope($"[Pid: {Process.Id}][CorrelationId: {correlationId}]"))
                    {
                        if (_stoppingStatus.Contains(_status) || token.IsCancellationRequested)
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

            await Task.CompletedTask;
        }

        internal async virtual Task<PipeStreamWrapper> CreateOperationPipeAsync(string pipeName)
        {
            var _workerOperationPipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
               PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough, 0, 0);
            await _workerOperationPipe.WaitForConnectionAsync().ConfigureAwait(false);
            return new PipeStreamWrapper(_workerOperationPipe);
        }

        /// <summary>
        /// Starts the external process and begins reading from its error output stream.
        /// </summary>
        private void StartProcess()
        {
            Process.Start();
            Process.BeginErrorReadLine();
            Process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Logger.LogError($"Procees Error Information:{e.Data}");
                }
            };

            _status = WorkerStatus.Running;
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
                    Headers = e.BasicProperties.Headers,
                    OriginalQueueName = _workerSetting.QueueName,
                };
                await _pipeDataStream.WriteAsync(task).ConfigureAwait(false);

                var taskOutput = await ReadAndProcessOutputAsync(token);

                if (_messageDoneMap.Contains(taskOutput.Status))
                {
                    AcknowledgeMessage(e.DeliveryTag);
                    string replyQueue = !string.IsNullOrWhiteSpace(taskOutput.ReplyQueueName) ? taskOutput.ReplyQueueName : e.BasicProperties.ReplyTo;
                    //push to another queue
                    ReplyQueue(replyQueue, e, taskOutput);
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
        /// Sends a reply message to a queue specified in the original message's reply-to property.
        /// </summary>
        /// <param name="e">Delivery event arguments containing the message details.</param>
        /// <param name="taskOutput">Output task from the external process.</param>
        private void ReplyQueue(string replyQueueName, BasicDeliverEventArgs e, MessageOutputTask taskOutput)
        {
            if (!string.IsNullOrWhiteSpace(replyQueueName) &&
                taskOutput.Status == MessageStatus.MESSAGE_DONE_WITH_REPLY)
            {
                Logger.LogDebug($"reply queue request reply queue name is {replyQueueName},replyMessage : {taskOutput.Message}");

                var properties = e.BasicProperties;
                properties.ContentEncoding = Encoding.UTF8.WebName;
                properties.Headers = taskOutput.Headers;

                //TODO! We could support let user fill queue or exchange name from worker protocol in future.
                channel.BasicPublish(string.Empty, replyQueueName, properties, Encoding.UTF8.GetBytes(taskOutput.Message));
            }
            //else
            //{
            //    replyQueueName = string.IsNullOrWhiteSpace(replyQueueName) ? "Empty" : replyQueueName;
            //    Logger.LogWarning($"reply queue name was setup as {replyQueueName}, but taskOutput status is {taskOutput.Status}");
            //}
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
                    taskOutput = await _pipeDataStream.ReadAsync<MessageOutputTask>().ConfigureAwait(false);
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
        protected virtual async Task GracefulReleaseAsync(CancellationToken token)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Gracefully shuts down the worker, ensuring all in-flight messages are processed or rejected.
        /// </summary>
        /// <param name="token">Cancellation token for stopping the shutdown process.</param>
        public async Task GracefulShutDownAsync(CancellationToken token)
        {
            using (Logger.BeginScope($"[Pid: {Process.Id}]"))
            {
                Logger.LogInformation("Executing GracefulShutDownAsync!");
                _status = WorkerStatus.Stopping;

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
                
                await CloseProcess();
                _status = WorkerStatus.Stopped;
                await GracefulReleaseAsync(token);
            }

            this.Dispose();
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

        private async Task CloseProcess()
        {
            //Sending close message
            Logger.LogInformation($"Begin WaitForExit free resource....");
            await Process.StandardInput.WriteLineAsync(MessageCommunicate.CLOSED_SIGNAL);
            await Process.StandardInput.FlushAsync();
            _pipeDataStream.Dispose();
            //to avoid some worker block in Console.ReadLine lead to program can't get down successfully
            while (!Process.WaitForExit(3000))
            {
                Logger.LogInformation("Process.WaitForExit.....");
            }
            Logger.LogInformation($"End WaitForExit and free resource....");
        }

        /// <summary>
        /// Disposes managed and unmanaged resources used by the RabbitMqWorker.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        /// <param name="disposing">Indicates whether to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed resources
                if (Process != null)
                {
                    Process.Close();
                    Process.Dispose();
                    Process = null;
                }

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

                if (_pipeDataStream != null)
                {
                    _pipeDataStream.Dispose();
                    _pipeDataStream = null;
                }

                if (_receivedWaitEvent != null)
                {
                    _receivedWaitEvent.Dispose();
                    _receivedWaitEvent = null;
                }

            }

            // Release unmanaged resources here if any

            _disposed = true;
        }
    }
}
