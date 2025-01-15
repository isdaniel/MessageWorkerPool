using System.IO.Pipes;

namespace ShareLib
{
    static class PipeClientFactory
    {
        public static async Task<NamedPipeClientStream> CreateAndConnectPipeAsync(string pipeName)
        {
            //var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            await pipe.ConnectAsync().ConfigureAwait(false);
            return pipe;
        }
    }
}

