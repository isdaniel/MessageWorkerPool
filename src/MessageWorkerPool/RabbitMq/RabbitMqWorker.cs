using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MessageWorkerPool.Utilities;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

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
        volatile int _messageCount = 0;
        internal IModel Channel { get; private set; }
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

        private readonly ConcurrentBag<ulong> _rejectMessageDeliveryTags = new ConcurrentBag<ulong>(){ };

        protected AutoResetEvent _receivedWaitEvent = new AutoResetEvent(false);

        /// <summary>
        /// Worker status
        /// </summary>
        public WorkerStatus Status { get; private set; } = WorkerStatus.WaitForInit;
        protected ILogger<RabbitMqWorker> Logger { get; }

        private bool _disposed = false;
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
            Channel = channel;
        }

        protected virtual IProcessWrapper CreateProcess(ProcessStartInfo processStartInfo) { 
        
            IProcessWrapper process = new ProcessWrapper(new Process
            {
                StartInfo = processStartInfo
            });

            return process;
        }

        /// <summary>
        /// Use standard input/output to communicate between worker Pool and worker.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual async Task InitWorkerAsync(CancellationToken token)
        {
            _consumer = new AsyncEventingBasicConsumer(Channel);
            Process = CreateProcess(new ProcessStartInfo()
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                FileName = _workerSetting.CommandLine,
                Arguments = _workerSetting.Arguments,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            });
            StartProcess();

            using (Logger.BeginScope($"[Pid: {Process.Id}]"))
            {

                Logger.LogInformation($"Setup Process!");
                ReceiveEvent = async (sender, e) =>
                {
                    var correlationId = e.BasicProperties.CorrelationId;
                    
                    using (Logger.BeginScope($"[Pid: {Process.Id}][CorrelationId: {correlationId}]"))
                    {
                        if (_stoppingStatus.Contains(Status) || token.IsCancellationRequested)
                        {
                            Logger.LogWarning($"doing GracefulShutDown reject message!");
                            //it should return, if the worker are processing GracefulShutDown.
                            _rejectMessageDeliveryTags.Add(e.DeliveryTag);
                            return;
                        }
                        Interlocked.Increment(ref _messageCount);
                        await ProcessingMessage(e, correlationId, token).ConfigureAwait(false);
                        Interlocked.Decrement(ref _messageCount);
                        _receivedWaitEvent.Set();
                    }
                };
                _consumer.Received += ReceiveEvent;
                Channel.BasicQos(0, Setting.PrefetchTaskCount, false);
                Channel.BasicConsume(_workerSetting.QueueName, false, _consumer);
                Logger.LogInformation($"Starting.. Channel ChannelNumber {Channel.ChannelNumber}");
            }

            await Task.CompletedTask;
        }

        private void StartProcess()
        {
            Process.Start();
            Process.BeginErrorReadLine();
            Process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Logger.LogError($"Procees Error Information:{e.Data}");
                }
            };
            Status = WorkerStatus.Running;
        }

        /// <summary>
        /// StandardInput: sending in message that get from MQ.
        /// StandardOutput: MESSAGE_DONE or MESSAGE_DONE_WITH_REPLY = Finish task, we can do BasicAck, otherwise will wait for signal that we can ack.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="correlationId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
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
                    Headers = e.BasicProperties.Headers
                };

                await Process.StandardInput.WriteLineAsync(task.ToJsonMessage()).ConfigureAwait(false);

                var taskOutput = await ReadAndProcessOutputAsync(token);

                if (_messageDoneMap.Contains(taskOutput.Status))
                {
                    AcknowledgeMessage(e.DeliveryTag);
                    //push to another queue
                    ReplyQueue(e, taskOutput);
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

        private void ReplyQueue(BasicDeliverEventArgs e, MessageOutputTask taskOutput)
        {
            if (!string.IsNullOrEmpty(e.BasicProperties.ReplyTo) &&
                taskOutput.Status == MessageStatus.MESSAGE_DONE_WITH_REPLY)
            {
                Logger.LogDebug($"reply queue request reply queue name is {e.BasicProperties.ReplyTo},replyMessage : {taskOutput.Message}");
                Channel.BasicPublish("", e.BasicProperties.ReplyTo, null, Encoding.UTF8.GetBytes(taskOutput.Message));
            }
        }

        private async Task<MessageOutputTask> ReadAndProcessOutputAsync(CancellationToken token)
        {
            var taskOutput = new MessageOutputTask() {
                Status = MessageStatus.IGNORE_MESSAGE
            };

            while (!token.IsCancellationRequested || Process.StandardOutput.Peek() > 0)
            {
                string responseJson = await Process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                Logger.LogDebug($"Message from worker process: {responseJson}");

                if (!string.IsNullOrEmpty(responseJson))
                {
                    try
                    {
                        taskOutput = JsonSerializer.Deserialize<MessageOutputTask>(responseJson);
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
            Channel.BasicAck(deliveryTag, false);
            Logger.LogDebug($"Channel ChannelNumber {Channel.ChannelNumber},Message {deliveryTag} acknowledged.");
        }

        private void RejectMessage(ulong deliveryTag)
        {
            Channel.BasicNack(deliveryTag, false, true);
            Logger.LogDebug($"Channel ChannelNumber {Channel.ChannelNumber},Message {deliveryTag} rejected.");
        }

        /// <summary>
        /// provide hock for sub-class implement 
        /// </summary>
        /// <returns></returns>
        protected virtual async Task GracefulReleaseAsync(CancellationToken token)
        {
            await Task.CompletedTask;
        }

        public async Task GracefulShutDownAsync(CancellationToken token)
        {
            using (Logger.BeginScope($"[Pid: {Process.Id}]"))
            {
                Logger.LogInformation("Executing GracefulShutDownAsync!");
                Status = WorkerStatus.Stopping;

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

                CloseProcess();
                Status = WorkerStatus.Stopped;
                await GracefulReleaseAsync(token);
            }
            
            this.Dispose();
            Logger.LogInformation("RabbitMQ Conn Closed!!!!");
        }

        private void RejectRemainingMessages()
        {
            Logger.LogInformation("Rejecting all remaining messages in the queue...");
            Logger.LogInformation($"messages {_rejectMessageDeliveryTags.Count} are waiting for rejecting from the queue.");
            foreach (var deliveryTag in _rejectMessageDeliveryTags)
            {
                RejectMessage(deliveryTag);
            }
            Logger.LogInformation("Rejected all remaining messages in the queue...");
        }

        private void CloseProcess()
        {
            //Sending close message
            Process.StandardInput.WriteLine(MessageCommunicate.CLOSED_SIGNAL);
            Logger.LogInformation($"Begin WaitForExit free resource....");
            Process.WaitForExit();
            Logger.LogInformation($"End WaitForExit and free resource....");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_disposed) {
                return;
            }

            if (Process != null)
            {
                Process.Dispose();
                Process.Close();
                Process = null;
            }

            if (Channel?.IsClosed != null)
            {
                Channel.Close();
                Channel = null;
            }

            _disposed = true;
        }
    }
}
