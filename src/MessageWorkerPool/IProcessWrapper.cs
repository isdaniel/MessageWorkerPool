using System.Diagnostics;
using System.IO;

namespace MessageWorkerPool
{
    public interface IProcessWrapper
    {
        bool Start();
        void BeginErrorReadLine();
        void WaitForExit();
        void Close();

        event DataReceivedEventHandler ErrorDataReceived;
        StreamWriter StandardInput { get; }
    }
}
