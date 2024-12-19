using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MessageWorkerPool
{
    public interface IWorkerPool : IDisposable
    {
        Task InitPoolAsync(CancellationToken token);
        Task WaitFinishedAsync(CancellationToken token);
    }
}
