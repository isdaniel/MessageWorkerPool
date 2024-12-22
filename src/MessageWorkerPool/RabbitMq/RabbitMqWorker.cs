using System;
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
        internal IModel Channle { get; private set; }
        protected IProcessWrapper Process { get; private set; }
        private readonly WorkerPoolSetting _workerSetting;
        private readonly ILoggerFactory _loggerFactory;
        private readonly HashSet<WorkerStatus> _stoppingStatus = new HashSet<WorkerStatus>(){
            WorkerStatus.Stopped,
            WorkerStatus.Stopping
        };

        //Message Finish Statuss
        private readonly HashSet<MessageStatus> _messgeDoneMap = new HashSet<MessageStatus>(){
            MessageStatus.MESSAGE_DONE,
            MessageStatus.MESSAGE_DONE_WITH_REPLY
        };

        /// <summary>
        /// Worker status
        /// </summary>
        public WorkerStatus Status { get; private set; } = WorkerStatus.WaitForInit;
        protected ILogger<RabbitMqWorker> Logger { get; }

        private bool _disposed = false;
        public RabbitMqWorker(
            RabbitMqSetting setting,
            WorkerPoolSetting workerSetting,
            IModel channle,
            ILoggerFactory loggerFactory)
        {
            if (workerSetting == null)
                throw new NullReferenceException(nameof(workerSetting));

            if (setting == null)
                throw new NullReferenceException(nameof(setting));

            _loggerFactory = loggerFactory;
            Logger = _loggerFactory.CreateLogger<RabbitMqWorker>();
            Setting = setting;
            _workerSetting = workerSetting;
            Channle = channle;
            Logger.LogInformation($"RabbitMq connection string: {setting.GetUriWithoutPassword()}");
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
            _consumer = new AsyncEventingBasicConsumer(Channle);
            Process = CreateProcess(new ProcessStartInfo()
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                FileName = _workerSetting.CommnadLine,
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
                            Channle.BasicNack(e.DeliveryTag, false, true);
                            Logger.LogWarning($"doing GracefulShutDown reject message!");
                        }

                        var message = Encoding.UTF8.GetString(e.Body.Span.ToArray());
                        Logger.LogInformation($"received message:{message}");
                        await ProcessingMessage(e, message, correlationId, token).ConfigureAwait(false);
                    }

                    await Task.Yield();
                };
                _consumer.Received += ReceiveEvent;
                Channle.BasicQos(0, Setting.PrefetchTaskCount, true);
                Channle.BasicConsume(Setting.QueueName, false, _consumer);
                Logger.LogInformation($"Worker running!");
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
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task ProcessingMessage(BasicDeliverEventArgs e, string message, string correlationId, CancellationToken token)
        {
            try
            {
                var task = new MessageInputTask
                {
                    Message = message,
                    CorrelationId = correlationId
                };

                await Process.StandardInput.WriteLineAsync(task.ToJsonMessage()).ConfigureAwait(false);

                var outputTask = new MessageOutputTask
                {
                    Status = MessageStatus.IGNORE_MESSAGE
                };

                outputTask = await ReadAndProcessOutputAsync(outputTask, token);

                if (_messgeDoneMap.Contains(outputTask.Status))
                {
                    AcknowledgeMessage(e);
                }
                else
                {
                    RejectMessage(e);
                }
            }
            catch (Exception ex)
            {
                RejectMessage(e);
                Logger.LogWarning(ex, "Processing message encountered an exception!");
            }
        }

        private async Task<MessageOutputTask> ReadAndProcessOutputAsync(MessageOutputTask outputTask, CancellationToken token)
        {
            while (!token.IsCancellationRequested || Process.StandardOutput.Peek() > 0)
            {
                string responseJson = await Process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                Logger.LogInformation($"Message from worker process: {responseJson}");

                if (!string.IsNullOrEmpty(responseJson))
                {
                    try
                    {
                        outputTask = JsonSerializer.Deserialize<MessageOutputTask>(responseJson);
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

                if (_messgeDoneMap.Contains(outputTask.Status))
                {
                    break;
                }
            }

            return outputTask;
        }

        private void AcknowledgeMessage(BasicDeliverEventArgs e)
        {
            Channle.BasicAck(e.DeliveryTag, false);
            Logger.LogInformation($"Message {e.DeliveryTag} acknowledged.");
        }

        private void RejectMessage(BasicDeliverEventArgs e)
        {
            Channle.BasicNack(e.DeliveryTag, false, true);
            Logger.LogInformation($"Message {e.DeliveryTag} rejected.");
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
            Logger.LogInformation("Exeuceting GracefulShutDownAsync!");
            Status = WorkerStatus.Stopping;
            _consumer.Received -= ReceiveEvent;
            ReceiveEvent = null;
            CloseProcess();
            Status = WorkerStatus.Stopped;
            await GracefulReleaseAsync(token);
            this.Dispose();
            Logger.LogInformation("RabbitMQ Conn Closed!!!!");
        }

        private void CloseProcess()
        {
            //Sending close message
            Process.StandardInput.WriteLine(MessageCommunicate.CLOSED_SIGNAL);
            using (Logger.BeginScope($"[Pid: {Process.Id}]")) {
                Logger.LogInformation($"Begin WaitForExit free resource....");
                Process.WaitForExit();
                Logger.LogInformation($"End WaitForExit and free resource....");
            }
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

            if (Channle?.IsClosed != null)
            {
                Channle.Close();
                Channle = null;
            }

            _disposed = true;
        }
    }
}
