using System;
using System.Threading.Tasks;
using MessageWorkerPool.Utilities;

namespace ShareLib
{
    public class MessageProcessor
    {
        
        public MessageProcessor()
        {
        }

        public async Task DoWorkAsync(Func<MessageInputTask, Task<MessageOutputTask>> process)
        {
            Console.WriteLine("worker starting...".ToIgnoreMessage());
            Console.WriteLine("Enter text 'quit' to stop:".ToIgnoreMessage());
            while (true)
            {
                var input = Console.ReadLine();
                if (input.Equals(MessageCommunicate.CLOSED_SIGNAL, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Exiting program.".ToIgnoreMessage());
                    break;
                }

                try
                {
                    MessageInputTask task = input.ToMessageInputTask();
                    
                    if (task == null)
                    {
                        //todo handle...
                    }
                    else
                    {
                        var res = await process(task).ConfigureAwait(false);
                        Console.WriteLine(res.ToJson());
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Worker occur unexpected error: {ex.ToString()}");
                    //we could requeue to another queue (error queue..ect), when we support rpc.
                    Console.WriteLine(new MessageOutputTask() {
                        Message = ex.Message,
                        Status = MessageStatus.MESSAGE_DONE
                    }.ToJson());
                }
            }
        }

        public void DoWork(Func<MessageInputTask, MessageOutputTask> process)
        {
            Func<MessageInputTask, Task<MessageOutputTask>> asyncProcess = task =>
                Task.FromResult(process(task));

            DoWorkAsync(asyncProcess).GetAwaiter().GetResult();
        }
    }
}

