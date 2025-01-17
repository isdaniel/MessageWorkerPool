using System.IO.Pipes;
using System.Threading.Tasks;

namespace MessageWorkerPool.Utilities
{
    public enum WorkerStatus
    {
        WaitForInit,
        Running,
        Stopping,
        Stopped
    }
}
