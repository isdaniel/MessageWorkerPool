using System.Text.Json;
using MessageWorkerPool.Utilities;
using ShareLib;

MessageProcessor processor = new MessageProcessor();
await processor.InitialAsync();
await processor.DoWorkAsync(async (task,cancelToken) =>
{

    var model = JsonSerializer.Deserialize<CountorModel>(task.Message);

    //do logical
    int i = model.StartVallue;
    int batchEnd = Math.Min(model.StartVallue + model.BatchExecutedCount, model.TotalCount);

  
    for (; i <= batchEnd && !cancelToken.IsCancellationRequested; i++) {
        //mock interrupt signal.
        try
        {
            await Task.Delay(3, cancelToken);
        }
        catch (TaskCanceledException)
        {
            //interrupt by cancellation token like a context switch.
            //Console.WriteLine("Task was canceled.");
        }

        model.CurrentSum += i;
    }
    
    if (i - 1 == model.TotalCount)
    {
        return new MessageOutputTask()
        {
            Message = model.CurrentSum.ToString(),
            Status = MessageStatus.MESSAGE_DONE_WITH_REPLY
        };
    }

    model.StartVallue = i;

    //Didn't finish task, then store status for next executing.
    return new MessageOutputTask()
    {
        Message = JsonSerializer.Serialize(model),
        Status = MessageStatus.MESSAGE_DONE_WITH_REPLY,
        ReplyQueueName = task.OriginalQueueName,
        Headers = task.Headers
    };
}).ConfigureAwait(false);

public class CountorModel
{
    public int StartVallue { get; set; }
    public int CurrentSum{ get; set; }
    public int BatchExecutedCount { get; set; }
    public int TotalCount { get; set; }
}
