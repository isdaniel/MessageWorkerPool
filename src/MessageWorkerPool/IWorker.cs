using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace MessageWorkerPool
{
    public interface IWorker
    {
        void CreateWorkByUnit(CancellationToken token);
        Task GracefulShutDownAsync(CancellationToken token);
    }
}
