using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
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
        private IModel _channle;
        private IProcessWrapper _process;
        private readonly WorkerPoolSetting _workerSetting;
        private readonly ILoggerFactory _loggerFactory;
        private readonly HashSet<WorkerStatus> _stoppingStatus = new HashSet<WorkerStatus>(){
            WorkerStatus.Stopped,
            WorkerStatus.Stopping
        };

        private readonly HashSet<MessageStatus> _messgeDoneMap = new HashSet<MessageStatus>(){
            MessageStatus.MESSAGE_DONE,
            MessageStatus.MESSAGE_DONE_WITH_REPLY
        };

        //MESSAGE_DONE,
        //MESSAGE_DONE_WITH_REPLY

        /// <summary>
        /// Worker status
        /// </summary>
        public WorkerStatus Status { get; private set; } = WorkerStatus.WaitForInit;
        protected ILogger<RabbitMqWorker> _logger { get; }

        private bool _disposed = false;
        public RabbitMqWorker(
            RabbitMqSetting setting,
            WorkerPoolSetting workerSetting,
            IModel channle,
            ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<RabbitMqWorker>();
            Setting = setting;
            _workerSetting = workerSetting;
            _channle = channle;
            _logger.LogInformation($"RabbitMq connection string: {setting.GetUriWithoutPassword()}");
        }

        private IProcessWrapper SetUpProcess()
        {
            IProcessWrapper process = CreateProcess(new ProcessStartInfo()
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

            process.Start();
            process.BeginErrorReadLine();
            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogError($"Procees Error Information:{e.Data}");
                }
            };

            return process;
        }

        internal virtual IProcessWrapper CreateProcess(ProcessStartInfo processStartInfo)
        {
            var process = new Process
            {
                StartInfo = processStartInfo
            };

            return new ProcessWrapper(process);
        }
        /// <summary>
        /// Use standard input/output to communicate between worker Pool and worker.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual async Task InitWorkerAsync(CancellationToken token)
        {
            _consumer = new AsyncEventingBasicConsumer(_channle);
            _process = SetUpProcess();
            Status = WorkerStatus.Running;

            using (_logger.BeginScope($"[Pid: {_process.Id}]"))
            {

                _logger.LogInformation($"Setup Process!");
                ReceiveEvent = async (sender, e) =>
                {
                    var correlationId = e.BasicProperties.CorrelationId;

                    using (_logger.BeginScope($"[Pid: {_process.Id}][CorrelationId: {correlationId}]"))
                    {
                        if (_stoppingStatus.Contains(Status))
                        {
                            _channle.BasicNack(e.DeliveryTag, false, true);
                            _logger.LogWarning($"doing GracefulShutDown reject message!");
                        }

                        var message = Encoding.UTF8.GetString(e.Body.Span.ToArray());
                        _logger.LogInformation($"received message:{message}");
                        await ProcessingMessage(e, message, correlationId).ConfigureAwait(false);
                    }

                    await Task.Yield();
                };
                _consumer.Received += ReceiveEvent;
                _channle.BasicQos(0, Setting.PrefetchTaskCount, true);
                _channle.BasicConsume(Setting.QueueName, false, _consumer);
                _logger.LogInformation($"Worker running!");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// StandardInput: sending in message that get from MQ.
        /// StandardOutput: MESSAGE_DONE or MESSAGE_DONE_WITH_REPLY = Finish task, we can do BasicAck, otherwise will wait for signal that we can ack.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task ProcessingMessage(BasicDeliverEventArgs e, string message,string correlationId)
        {
            try
            {
                var task = new MessageInputTask() {
                    Message = message,
                    CorrelationId = correlationId
                };

                await _process.StandardInput.WriteLineAsync(task.ToJsonMessage()).ConfigureAwait(false);

                var outputTask = new MessageOutputTask() {
                    Stauts = MessageStatus.IGNORE_MESSAGE
                };

                //Wait for MESSAGE_DONE then we can ack message
                //use protocol by JSON format.
                do
                {
                    string responseJson = await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    _logger.LogInformation($"message from worker process:{responseJson}");
                    if (!string.IsNullOrEmpty(responseJson))
                    {
                        try
                        {
                            outputTask = JsonSerializer.Deserialize<MessageOutputTask>(responseJson);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "MessageOutputTask Json Parsing error");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Json Parsing Unexpect Error");
                            throw;
                        }
                    }
                    
                } while (_messgeDoneMap.Contains(outputTask.Stauts));

                _channle.BasicAck(e.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _channle.BasicNack(e.DeliveryTag, false, true);
                _logger.LogWarning(ex, "processing message occur exception!");
            }
        }

        /// <summary>
        /// provide hock for sub-class implement 
        /// </summary>
        /// <returns></returns>
        public virtual async Task GracefulReleaseAsync(CancellationToken token)
        {
            await Task.CompletedTask;
        }

        public async Task GracefulShutDownAsync(CancellationToken token)
        {
            _logger.LogInformation("Exeuceting GracefulShutDownAsync!");
            Status = WorkerStatus.Stopping;
            _consumer.Received -= ReceiveEvent;
            ReceiveEvent = null;
            CloseProcess();
            Status = WorkerStatus.Stopped;
            await GracefulReleaseAsync(token);
            this.Dispose();
            _logger.LogInformation("RabbitMQ Conn Closed!!!!");
        }

        private void CloseProcess()
        {
            //Sending close message
            _process.StandardInput.WriteLine(MessageCommunicate.CLOSED_SIGNAL);
            using (_logger.BeginScope($"[Pid: {_process.Id}]")) {
                _logger.LogInformation($"Begin WaitForExit free resource....");
                _process.WaitForExit();
                _logger.LogInformation($"End WaitForExit and free resource....");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                return;
            }

            if (disposing) {
                if (_process != null)
                {
                    _process.Dispose();
                    _process.Close();
                    _process = null;
                }
            }

            if (_channle?.IsClosed != null)
            {
                _channle.Close();
                _channle = null;
            }

            _disposed = true;
        }
    }
}
