using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private readonly ILogger<ProcessPool> _logger;
        private readonly BlockingCollection<MessageTask> _taskQueue;
        internal readonly List<Task> _workers = new List<Task>();
        public int ProcessCount { get; private set; }
        private volatile bool _finish = false;
        public bool IsFinish  => _finish;

        internal readonly List<IProcessWrapper> _processList = new List<IProcessWrapper>();

        public ProcessPool(PoolSetting poolSetting, ILoggerFactory loggerFactory)
        {
            if (poolSetting == null)
                throw new NullReferenceException(nameof(poolSetting));

            if (loggerFactory == null)
                throw new NullReferenceException(nameof(poolSetting));

            if (string.IsNullOrEmpty(poolSetting.CommnadLine))
                throw new ArgumentNullException($"Commnad line can't be null {nameof(poolSetting.CommnadLine)}");

            ProcessCount = poolSetting.WorkerUnitCount;
            this._poolSetting = poolSetting;
            this._logger = loggerFactory.CreateLogger<ProcessPool>();
            _taskQueue = new BlockingCollection<MessageTask>(poolSetting.WorkerUnitCount);
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


        public Task<bool> AddTaskAsync(MessageTask task)
        {
            bool result = false; 
            if (!_finish && !_taskQueue.IsCompleted)
            {
                _taskQueue.Add(task);
                result = true;
            }
            return Task.FromResult(result);
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
