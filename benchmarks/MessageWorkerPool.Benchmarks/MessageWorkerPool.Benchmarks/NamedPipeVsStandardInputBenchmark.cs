using BenchmarkDotNet.Attributes;
using System.Text.Json;
using System.IO.Pipes;
using System.Text;
using MessageWorkerPool.IO;
using System.Net;

//[RPlotExporter]
//[MemoryDiagnoser]
public class NamedPipeVsStandardInputBenchmark
{
    private NamedPipeServerStream _pipeServer;
    private NamedPipeClientStream _pipeClient;
    private const int Iterations = 10000;

    [GlobalSetup]
    public async Task Setup()
    {
        // Initialize the named pipe with a unique name
        string pipeName = $"TestPipe-{Guid.NewGuid()}";
        _pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte);
        _pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

        var serverTask = Task.Run(async () =>
        {
            await _pipeServer.WaitForConnectionAsync();
        });

        await _pipeClient.ConnectAsync();
        await serverTask;
        Console.WriteLine("Pipe connected..");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pipeServer?.Dispose();
        _pipeClient?.Dispose();
    }

    [Benchmark(Description = "NamedPipeStream (Standard Json)")]
    public async Task PipeBenchmarkTest()
    {
        var serverTask = Task.Run(async () =>
        {
            for (int i = 0; i < Iterations; i++)
            {
                var model = await ReadAsync<TestModle>();
            }
        });

        for (int i = 0; i < Iterations; i++)
        {
            await WriteAsync(new TestModle()
            {
                Message = $"Hello, BenchmarkDotNet! [{i}]"
            });
        }

        await serverTask;

    }

    private async Task<TModel> ReadAsync<TModel>() where TModel : class
    {
        var len = await ReadLengthAsync();

        if (len == 0)
        {
            return default(TModel);
        }

        var data = new byte[len];
        await _pipeServer.ReadAsync(data, 0, len);        
        return JsonSerializer.Deserialize<TModel>(data);
    }

    private async Task<int> ReadLengthAsync()
    {
        const int lenSize = sizeof(int);
        var lengthBuffer = new byte[lenSize];
        var bytesRead = await _pipeServer.ReadAsync(lengthBuffer, 0, lenSize);

        if (bytesRead != lenSize)
            throw new IOException($"Expected {lenSize} bytes but read {bytesRead}");

        return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
    }

    private  async Task WriteAsync<TModel>(TModel obj)
          where TModel : class
    {
        var data = JsonSerializer.Serialize(obj);
        var buffer = Encoding.UTF8.GetBytes(data);
        await WriteLengthAsync(buffer.Length);
        await _pipeClient.WriteAsync(buffer, 0, buffer.Length);
        await _pipeClient.FlushAsync();
    }

    private async Task WriteLengthAsync(int len)
    {
        var lenbuf = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(len));
        await _pipeClient.WriteAsync(lenbuf, 0, lenbuf.Length);
    }

    [Benchmark(Description = "NamedPipeStream (MessagePackSerializer)")]
    public async Task PipeStreamWrapperBenchmark()
    {
        var pipeServerStream = new PipeStreamWrapper(_pipeServer);
        var pipeClientStream = new PipeStreamWrapper(_pipeClient);

        var clientTask = Task.Run(async () =>
        {
            for (int i = 0; i < Iterations; i++)
            {
                await pipeClientStream.ReadAsync<TestModle>().ConfigureAwait(false);
            }
        });

        for (int i = 0; i < Iterations; i++)
        {
            await pipeServerStream.WriteAsync(new TestModle()
            {
                Message = $"Hello, BenchmarkDotNet! [{i}]"
            }).ConfigureAwait(false);
        }

        await clientTask;
    }
}
