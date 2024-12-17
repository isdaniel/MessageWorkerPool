using MessageWorkerPool.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
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
    public abstract class WorkerPoolBase : IWorkerPool
    {
        public ushort ProcessCount { get; }

        protected readonly WorkerPoolSetting _workerSetting;
        protected readonly ILogger<WorkerPoolBase> _logger;
        protected readonly ILoggerFactory _loggerFactory;
        internal readonly List<Task> _workerTasks = new List<Task>();
        private bool _isClosed = false;
        public bool IsClosed  => _isClosed;
        protected readonly List<IWorker> Workers = new List<IWorker>();
        private bool _disposed = false;

        public WorkerPoolBase(WorkerPoolSetting workerSetting,ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            this._logger = _loggerFactory.CreateLogger<WorkerPoolBase>();
            _workerSetting = workerSetting;
            ProcessCount = workerSetting.WorkerUnitCount;
        }

        /// <summary>
        /// Implement message processor worker creation (current support rabbitMq, we can also implement kafka worker...etc)
        /// </summary>
        /// <returns></returns>
        protected abstract IWorker GetWorker();

        public async Task InitPoolAsync(CancellationToken token)
        {
            for (int i = 0; i < ProcessCount; i++)
            {
                IWorker worker = GetWorker();
                this._workerTasks.Add(Task.Run(async () =>
                {
                    await worker.InitWorkerAsync(token);
                }));
                Workers.Add(worker);
            }

            await Task.CompletedTask;
        }

        public async Task WaitFinishedAsync(CancellationToken token)
        {
            foreach (var worker in Workers)
            {
                await worker.GracefulShutDownAsync(token);
            }

            await Task.WhenAll(_workerTasks.ToArray());
            Dispose();   
            _isClosed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var worker in Workers)
            {
                worker.Dispose();
            }

            _disposed = true;
        }
    }
}
