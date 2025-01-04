using System.Text.Json;
using MessageWorkerPool.Utilities;
using ShareLib;

MessageProcessor processor = new MessageProcessor();
FibonacciService service = new FibonacciService();
await processor.DoWorkAsync(async (task) =>
{
    var model = JsonSerializer.Deserialize<FibonacciModel>(task.Message);
    return new MessageOutputTask()
    {
        Message = JsonSerializer.Serialize(service.Calculation(model.Value)),
        Status = MessageStatus.MESSAGE_DONE_WITH_REPLY
    };
}).ConfigureAwait(false);

class FibonacciService {
    Dictionary<int, int> _map = new Dictionary<int, int>();
    public int Calculation(int n)
    {
        if (n == 0 || n == 1)
        {
            return n;
        }
        else if (_map.TryGetValue(n, out var val))
        {
            return val;
        }

        _map[n] = Calculation(n - 1) + Calculation(n - 2);

        return _map[n];
    }
}


public class FibonacciModel
{
    public int Value { get; set; }
}
