using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MessageWorkerPool.RabbitMq;

namespace MessageWorkerPool
{
    public class WorkerPoolService : BackgroundService
    {
        private readonly ILogger<WorkerPoolService> _logger;
        private readonly IWorker _worker;

        public WorkerPoolService(ILogger<WorkerPoolService> logger, IWorker worker)
        {
            this._worker = worker;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken token)
        {
            _worker.CreateWorkByUnit(token);
            token.WaitHandle.WaitOne();
            _logger.LogInformation("ExecuteAsync Finish!");
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start Stop...Wait for queue to comsume stop.");
            await _worker.GracefulShutDownAsync(cancellationToken);
            _logger.LogInformation("Stop Service.");
            await base.StopAsync(cancellationToken);
        }
    }
}
