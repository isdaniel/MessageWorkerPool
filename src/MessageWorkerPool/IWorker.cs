using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace MessageWorkerPool
{
    public interface IWorker : IDisposable
    {
        Task InitWorkerAsync(CancellationToken token);
        Task GracefulShutDownAsync(CancellationToken token);
    }
}
