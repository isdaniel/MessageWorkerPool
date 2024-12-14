using System.Diagnostics;
using System.IO;

namespace MessageWorkerPool
{
    public class ProcessWrapper : IProcessWrapper
    {
        private readonly Process _process;

        public StreamWriter StandardInput => _process.StandardInput;

        public ProcessWrapper(Process process)
        {
            _process = process;
            _process.ErrorDataReceived += (sender, args) =>
            {
                ErrorDataReceived?.Invoke(sender, args);
            };
        }

        public bool Start() => _process.Start();

        public void WaitForExit() => _process.WaitForExit();

        public void BeginErrorReadLine()
        {
            _process.BeginErrorReadLine();
        }

        public void Close()
        {
            _process.Close();
        }

        public event DataReceivedEventHandler ErrorDataReceived;
    }
}
