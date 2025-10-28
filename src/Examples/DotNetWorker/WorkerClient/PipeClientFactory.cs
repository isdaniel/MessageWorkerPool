using System.Threading.Tasks;
using System.IO.Pipes;

namespace WorkerClient
{
    public static class PipeClientFactory
    {
        public static async Task<NamedPipeClientStream> CreateAndConnectPipeAsync(string pipeName)
        {
            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            await client.ConnectAsync().ConfigureAwait(false);
            return client;
        }
    }
}
