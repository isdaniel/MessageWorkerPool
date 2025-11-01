using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MessageWorkerPool.Utilities;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using MessageWorkerPool.IO;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.Telemetry.Abstractions;

namespace MessageWorkerPool
{
    public abstract class WorkerBase : IWorker
    {
        /// <summary>
        /// Worker status
        /// </summary>
        private volatile WorkerStatus _status = WorkerStatus.WaitForInit;
        public WorkerStatus Status
        {
            get { return _status; }
        }

        private PipeStreamWrapper _pipeDataStream;
        protected readonly HashSet<WorkerStatus> _stoppingStatus = new HashSet<WorkerStatus>(){
            WorkerStatus.Stopped,
            WorkerStatus.Stopping
        };

        //Message Finish Statuss
        protected readonly HashSet<MessageStatus> _messageDoneMap = new HashSet<MessageStatus>(){
            MessageStatus.MESSAGE_DONE,
            MessageStatus.MESSAGE_DONE_WITH_REPLY
        };

        protected IProcessWrapper Process { get; private set; }

        /// <summary>
        /// Gets the process ID for telemetry purposes.
        /// </summary>
        public int? ProcessId => Process?.Id;

        protected readonly WorkerPoolSetting _workerSetting;
        protected readonly ITelemetryManager _telemetryManager;
        protected AutoResetEvent _receivedWaitEvent = new AutoResetEvent(false);

        /// <summary>
        /// Gets the worker ID (process ID as string).
        /// </summary>
        public string WorkerId => Process?.Id.ToString() ?? "unknown";

        protected WorkerBase(
            WorkerPoolSetting workerSetting,
            ILogger logger,
            ITelemetryManager telemetryManager = null)
        {
            _workerSetting = workerSetting;
            Logger = logger;
            _telemetryManager = telemetryManager ?? new TelemetryManager(NoOpTelemetryProvider.Instance);
        }

        protected ILogger Logger { get; }

        private bool _disposed = false;

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

                using (var activity = _telemetryManager.StartShutdownActivity(WorkerId))
                {
                    try
                    {
                        await GracefulReleaseAsync(token);
                        await CloseProcess();
                        _status = WorkerStatus.Stopped;
                    }
                    catch (Exception ex)
                    {
                        _telemetryManager.RecordException(activity, ex);
                        throw;
                    }
                }
            }

            Dispose();
        }

        /// <summary>
        /// provide hock for sub-class implement
        /// </summary>
        /// <returns></returns>
        protected virtual async Task GracefulReleaseAsync(CancellationToken token)
        {
            await Task.CompletedTask;
        }

        private async Task CloseProcess()
        {
            //Sending close message
            Logger.LogInformation($"Begin WaitForExit free resource....");
            await SendingDataToWorker(MessageCommunicate.CLOSED_SIGNAL);
            _pipeDataStream.Dispose();
            //to avoid some worker block in Console.ReadLine lead to program can't get down successfully
            while (!Process.WaitForExit(3000))
            {
                Logger.LogInformation("Process.WaitForExit.....");
            }
            Logger.LogInformation($"End WaitForExit and free resource....");
        }

        protected abstract void SetupMessageQueueSetting(CancellationToken token);

        /// <summary>
        /// Initializes the worker, setting up the external process and RabbitMQ consumer.
        /// </summary>
        /// <param name="token">Cancellation token for stopping the initialization.</param>
        public async Task InitWorkerAsync(CancellationToken token)
        {
            using (var activity = _telemetryManager.StartWorkerInitActivity("pending", _workerSetting.QueueName))
            {
                try
                {
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
                        activity?.SetTag("worker.id", WorkerId);
                        activity?.SetTag("process.id", Process.Id);

                        await InitialDataStreamPipeAsync().ConfigureAwait(false);
                        SetupMessageQueueSetting(token);
                    }
                }
                catch (Exception ex)
                {
                    _telemetryManager.RecordException(activity, ex);
                    throw;
                }
            }
        }

        private async Task InitialDataStreamPipeAsync()
        {
            Logger.LogInformation($"Setup Process!");
            var pipeName = $"pipeDataStream_{Guid.NewGuid().ToString("N")}";
            var creatiepipeTask = CreateOperationPipeAsync(pipeName).ConfigureAwait(false);
            await SendingDataToWorker(pipeName).ConfigureAwait(false);
            Logger.LogInformation($"data pipe create successfully: {pipeName}");
            //must wait after sent pipeName to woker-process
            _pipeDataStream = await creatiepipeTask;
        }

        /// <summary>
        /// Starts the external process and begins reading from its error output stream.
        /// </summary>
        private void StartProcess()
        {
            Process.Start();
            Process.BeginErrorReadLine();
            Process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Logger.LogError($"Procees Error Information:{e.Data}");
                }
            };

            _status = WorkerStatus.Running;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Process?.Close();
                    Process?.Dispose();
                    Process = null;

                    _pipeDataStream?.Dispose();
                    _pipeDataStream = null;
                }

                if (_receivedWaitEvent != null)
                {
                    _receivedWaitEvent.Dispose();
                    _receivedWaitEvent = null;
                }

                _disposed = true;
            }
        }

        private async Task SendingDataToWorker(string command)
        {
            await Process.StandardInput.WriteLineAsync(command).ConfigureAwait(false);
            await Process.StandardInput.FlushAsync().ConfigureAwait(false);
        }

        protected async virtual Task<PipeStreamWrapper> CreateOperationPipeAsync(string pipeName,CancellationToken token = default)
        {
            try
            {
                var _workerOperationPipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                   PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough, 0, 0);
                await _workerOperationPipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                return new PipeStreamWrapper(_workerOperationPipe);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to create operation pipe with name {pipeName}");
                throw;
            }
        }

        /// <summary>
        /// Disposes managed and unmanaged resources used by the MqWorker.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected async Task DataStreamWriteAsync(MessageInputTask task)
        {
            await _pipeDataStream.WriteAsync(task).ConfigureAwait(false);
        }

        protected async Task<T> DataStreamReadAsync<T>() where T : class
        {
            return await _pipeDataStream.ReadAsync<T>().ConfigureAwait(false);
        }

        /// <summary>
        /// Starts the external process and begins reading from its error output stream.
        /// </summary>
        protected virtual IProcessWrapper CreateProcess(ProcessStartInfo processStartInfo)
        {
            IProcessWrapper process = new ProcessWrapper(new Process
            {
                StartInfo = processStartInfo
            });

            return process;
        }

        /// <summary>
        /// Sends a reply message to a queue specified in the original message's reply-to property.
        /// </summary>
        protected async Task ReplyQueueAsync(string replyQueueName, MessageOutputTask taskOutput, Delegate publishAction)
        {
            if (!string.IsNullOrWhiteSpace(replyQueueName) && taskOutput.Status == MessageStatus.MESSAGE_DONE_WITH_REPLY)
            {
                Logger.LogDebug($"reply queue request reply queue name is {replyQueueName},replyMessage : {taskOutput.Message}");

                if (publishAction is Func<Task> asyncAction)
                {
                    await asyncAction();
                }
                else if (publishAction is Action syncAction)
                {
                    syncAction();
                }
            }
            else if (taskOutput.Status != MessageStatus.MESSAGE_DONE_WITH_REPLY && !string.IsNullOrWhiteSpace(replyQueueName))
            {
                Logger.LogWarning($"reply queue name was setup as {replyQueueName}, but taskOutput status is {taskOutput.Status}");
            }
            else if (taskOutput.Status == MessageStatus.MESSAGE_DONE_WITH_REPLY && string.IsNullOrWhiteSpace(replyQueueName))
            {
                Logger.LogWarning($"reply queue name is null or empty, but taskOutput status is {taskOutput.Status}");
            }
        }
    }
}
