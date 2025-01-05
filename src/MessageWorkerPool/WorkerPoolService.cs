using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool
{

    public class WorkerPoolService : BackgroundService
    {
        private readonly ILogger<WorkerPoolService> _logger;
        private readonly WorkerPoolSetting[] _workerSettings;
        private readonly IWorkerPoolFactory _workerPoolFacorty;
        private readonly ILoggerFactory _loggerFactory;
        private readonly List<IWorkerPool> _workerPools;

        public WorkerPoolService(WorkerPoolSetting[] workerSettings, IWorkerPoolFactory workerPoolFacorty, ILoggerFactory loggerFactory, ILogger<WorkerPoolService> logger)
        {
            _logger = logger;
            _workerSettings = workerSettings;
            _workerPoolFacorty = workerPoolFacorty;
            _loggerFactory = loggerFactory;
            _workerPools = new List<IWorkerPool>();
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            foreach (var workerSetting in _workerSettings)
            {
                var workerPool = _workerPoolFacorty.CreateWorkerPool(workerSetting);
                await workerPool.InitPoolAsync(token);
                _workerPools.Add(workerPool);
            }

            _logger.LogInformation("WorkerPool initialization Finish!");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start Stop...Wait for workers comsume task and stop.");
            foreach (var workerPool in _workerPools)
            {
                await workerPool.WaitFinishedAsync(cancellationToken);
            }
            _logger.LogInformation("Stop Service.");
            await base.StopAsync(cancellationToken);
        }
    }
}
