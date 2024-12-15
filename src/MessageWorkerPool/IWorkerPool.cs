using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MessageWorkerPool
{
    public interface IWorkerPool
    {
        Task<bool> AddTaskAsync(MessageTask task);
        Task WaitFinishedAsync(CancellationToken token);
    }
}
