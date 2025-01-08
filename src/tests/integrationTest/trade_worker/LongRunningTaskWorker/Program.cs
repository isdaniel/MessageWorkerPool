using System.Text.Json;
using MessageWorkerPool.Utilities;
using ShareLib;

const string PREVIOUSCONTEXT = "PREVIOUSCONTEXT";

MessageProcessor processor = new MessageProcessor();
await processor.DoWorkAsync(async (task) =>
{
    //write log with task.Headers[PREVIOUSCONTEXT]

    //if (!task.Headers.TryGetValue(PREVIOUSCONTEXT, out var taskJson))
    //{
    //    taskJson = task.Message;
    //}

    //Console.WriteLine($"model Json:{taskJson}".ToIgnoreMessage());

    var model = JsonSerializer.Deserialize<CountorModel>(task.Message);

    //do logical
    int i = model.StartVallue;
    int batchEnd = Math.Min(model.StartVallue + model.BatchExecutedCount, model.TotalCount);
    for (; i <= batchEnd; i++) {

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

    await Task.Delay(200);

    model.StartVallue = i;
    //task.Headers[PREVIOUSCONTEXT] = JsonSerializer.Serialize(model);

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
