using System;
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
        private readonly IWorker _worker;
        private readonly IHostApplicationLifetime _appLifetime;

        public WorkerPoolService(ILogger<WorkerPoolService> logger, IWorker worker, IHostApplicationLifetime appLifetime)
        {
            this._worker = worker;
            _appLifetime = appLifetime;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken token)
        {
            //_appLifetime.ApplicationStopping.Register(async () => {
            //    _logger.LogInformation("starting ApplicationStopping...!");
            //    await _worker.GracefulShutDownAsync(token);
            //});

            //Console.CancelKeyPress += async (sender, e) =>
            //{
            //    await _worker.GracefulShutDownAsync(token);
            //    _logger.LogInformation("Cancel!");
            //};

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
