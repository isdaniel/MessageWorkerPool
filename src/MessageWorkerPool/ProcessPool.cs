using MessageWorkerPool.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MessageWorkerPool
{
    /// <summary>
    /// Process Pool
    /// </summary>
    public class ProcessPool : IWorkerPool
    {
        public const string CLOSED_SIGNAL = "quit";
        private readonly PoolSetting _poolSetting;
        internal ILoggerFactory LoggerFactory { get; }
        private readonly ILogger<ProcessPool> _logger;
        private readonly BlockingCollection<MessageTask> _taskQueue;
        internal readonly List<Task> _workers = new List<Task>();
        public int ProcessCount { get; }
        private volatile bool _finish = false;
        public bool IsFinish  => _finish;
        private readonly List<IProcessWrapper> _processList = new List<IProcessWrapper>();

        public ProcessPool(PoolSetting poolSetting, ILoggerFactory loggerFactory)
        {
            if (poolSetting == null)
                throw new NullReferenceException(nameof(poolSetting));

            if (string.IsNullOrEmpty(poolSetting.CommnadLine))
                throw new ArgumentNullException($"Commnad line can't be null {nameof(poolSetting.CommnadLine)}");

            ProcessCount = poolSetting.WorkerUnitCount;
            this._poolSetting = poolSetting;
            LoggerFactory = loggerFactory ?? new NullLoggerFactory();
            this._logger = LoggerFactory.CreateLogger<ProcessPool>();
            int size = Utilities.MaxPowerOfTwo(poolSetting.WorkerUnitCount);
            _taskQueue = new BlockingCollection<MessageTask>(size);
            InitPool();
        }

        private void InitPool()
        {
            for (int i = 0; i < ProcessCount; i++)
            {
                var process = SetUpProcess();
                this._workers.Add(Task.Run(() =>
                {
                    ProcessHandler(process);
                    //signle to close process
                    process.StandardInput.WriteLine(CLOSED_SIGNAL);
                    int pid = process.Id;
                    _logger.LogInformation($"[Pid:{pid}] Begin WaitForExit free resource....");
                    process.WaitForExit();
                    process.Close();
                    _logger.LogInformation($"[Pid:{pid}] End WaitForExit and free resource....");
                }));
                _processList.Add(process);
            }
        }

        internal virtual IProcessWrapper CreateProcess(ProcessStartInfo processStartInfo)
        {
            var process = new Process
            {
                StartInfo = processStartInfo
            };

            return new ProcessWrapper(process);
        }

        private IProcessWrapper SetUpProcess()
        {
            IProcessWrapper process = CreateProcess(new ProcessStartInfo()
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                FileName = _poolSetting.CommnadLine,
                Arguments = _poolSetting.Arguments,
                CreateNoWindow = true
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

        public Task<bool> AddTaskAsync(MessageTask task, CancellationToken token)
        {
            if (_finish || token.IsCancellationRequested)
            {
                return Task.FromResult(false);
            }

            try
            {
                return Task.FromResult(_taskQueue.TryAdd(task, -1, token));
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Cancellation requested before adding task!");
                // Handle cancellation gracefully
                return Task.FromResult(false);
            }
        }

        private void ProcessHandler(IProcessWrapper process)
        {
            while (_taskQueue.TryTake(out var task, Timeout.InfiniteTimeSpan))
            {
                if (task != null)
                {
                    process.StandardInput.WriteLine(task.ToJsonMessage());
                }

                if (_finish && _taskQueue.IsCompleted)
                {
                    break;
                }
            }
        }

        public async Task WaitFinishedAsync(CancellationToken token)
        {
            _finish = true;
            _taskQueue.CompleteAdding();

            await Task.WhenAll(_workers.ToArray());
        }
    }
}
