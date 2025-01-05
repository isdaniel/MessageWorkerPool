using System.Text.Json;
using MessageWorkerPool.Utilities;
using ShareLib;

MessageProcessor processor = new MessageProcessor();
FibonacciService fibonacci = new FibonacciService();
await processor.DoWorkAsync(async (task) =>
{
    var model = JsonSerializer.Deserialize<FibonacciModel>(task.Message);
    return new MessageOutputTask()
    {
        Message = JsonSerializer.Serialize(fibonacci.Calculation(model.Value)),
        Status = MessageStatus.MESSAGE_DONE_WITH_REPLY
    };
}).ConfigureAwait(false);

class FibonacciService
{
    private readonly Dictionary<int, int> _map = new();

    public int Calculation(int n)
    {
        if (n == 0)
            return 0; 
        if (n == 1 || n == -1)
            return 1; 

        if (_map.TryGetValue(n, out var val))
        {
            return val;
        }

        if (n > 0)
        {
            _map[n] = Calculation(n - 1) + Calculation(n - 2);
        }
        else
        {
            _map[n] = Calculation(n + 2) - Calculation(n + 1); 
        }

        return _map[n];
    }
}


public class FibonacciModel
{
    public int Value { get; set; }
}
