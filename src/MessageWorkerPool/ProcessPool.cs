using Microsoft.Extensions.Logging;
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
        const string CLOSED_SIGNAL = "quit";
        private readonly PoolSetting _poolSetting;
        private readonly ILogger<ProcessPool> _logger;
        private BlockingCollection<MessageTask> _taskQueue;
        private List<Task> _workers = new List<Task>();
        private readonly int _processCount;
        private volatile bool _finish = false;
        private List<Process> _processList = new List<Process>();

        public ProcessPool(PoolSetting poolSetting, ILoggerFactory loggerFactory)
        {
            this._processCount = poolSetting.WorkUnitCount;
            this._poolSetting = poolSetting;
            this._logger = loggerFactory.CreateLogger<ProcessPool>();
            _taskQueue = new BlockingCollection<MessageTask>(poolSetting.WorkUnitCount);
            InitPool();
        }

        private void InitPool()
        {
            for (int i = 0; i < _processCount; i++)
            {
                var process = CreateProcess();
                this._workers.Add(Task.Run(() =>
                {
                    ProcessHandler(process);
                }));
                _processList.Add(process);
            }
        }

        private Process CreateProcess()
        {
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo()
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                FileName = _poolSetting.CommnadLine,
                Arguments = _poolSetting.Arguments,
                CreateNoWindow = true
            };
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
            if (!_finish)
            {
                _taskQueue.Add(task);
                result = true;
            }
            return Task.FromResult(result);
        }

        private void ProcessHandler(Process process)
        {
            while (true)
            {
                var task = _taskQueue.Take();

                if (task != null)
                    process.StandardInput.WriteLine(task.ToJsonMessage());

                if (_finish && _taskQueue.Count == 0)
                    break;
            }

            //signle to close process
            process.StandardInput.WriteLine(CLOSED_SIGNAL);
        }

        public async Task WaitFinishedAsync(CancellationToken token)
        {
            _finish = true;
            _taskQueue.CompleteAdding();
            foreach (var process in _processList)
            {
                process.WaitForExit();
            }

            await Task.WhenAll(_workers.ToArray());
        }
    }
}
