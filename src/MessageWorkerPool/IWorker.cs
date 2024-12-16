using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace MessageWorkerPool
{
    public interface IWorker
    {
        Task InitConnectionAsync(CancellationToken token);
        Task GracefulShutDownAsync(CancellationToken token);
    }
}
