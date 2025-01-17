using System;
using System.Diagnostics;
using System.IO;

namespace MessageWorkerPool
{
    public interface IProcessWrapper : IDisposable
    {
        bool Start();
        void BeginErrorReadLine();
        void WaitForExit();
        bool WaitForExit(int milliseconds);
        void Close();
        event DataReceivedEventHandler ErrorDataReceived;
        StreamWriter StandardInput { get; }
        StreamReader StandardOutput { get; }
        int Id { get; }
    }
}
