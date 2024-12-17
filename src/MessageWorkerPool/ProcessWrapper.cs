using System.Diagnostics;
using System.IO;

namespace MessageWorkerPool
{
    /// <summary>
    /// process wrapper, that provide more flexibility and we can do unit test
    /// </summary>
    public class ProcessWrapper : IProcessWrapper
    {
        private readonly Process _process;

        public StreamWriter StandardInput => _process.StandardInput;
        public StreamReader StandardOutput => _process.StandardOutput;

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
        public int Id => _process.Id;

        public void BeginErrorReadLine()
        {
            _process.BeginErrorReadLine();
        }

        public void Close()
        {
            _process.Close();
        }

        public void Dispose()
        {
            _process.Dispose();
        }

        public event DataReceivedEventHandler ErrorDataReceived;
    }
}
