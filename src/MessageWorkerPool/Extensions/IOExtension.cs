using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MessageWorkerPool.Extensions
{

    //current didn't support pipe async cancel task, we can implement in future.
    [ExcludeFromCodeCoverage]
    internal static class IOExtension
    {
        internal static Task<int> ReadPipeAsync(this PipeStream pipe, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<int>(cancellationToken);

            var registration = cancellationToken.Register(() => CancelPipeIo(pipe));

            var async = pipe.BeginRead(buffer, offset, count, null, null);

            return new Task<int>(() =>
            {
                try { return pipe.EndRead(async); }
                finally { registration.Dispose(); }
            }, cancellationToken);
        }

        internal static void CancelPipeIo(PipeStream pipe)
        {
            // Note: no PipeStream.IsDisposed, we'll have to swallow
            try
            {
                CancelIo(pipe.SafePipeHandle);
            }
            catch (ObjectDisposedException) { }
        }
        [DllImport("kernel32.dll")]
        internal static extern bool CancelIo(SafePipeHandle handle);
    }
}
