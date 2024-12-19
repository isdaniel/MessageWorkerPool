using System;
using MessageWorkerPool.Utilities;

namespace WorkerProcessSample
{
    public class MessageProcessor
    {
        
        public MessageProcessor()
        {
        }

        public void DoWork(Func<MessageInputTask, MessageOutputTask> process)
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
                        var res = process(task);
                        Console.WriteLine(res.ToJson());
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Json Parse Error: {ex.ToString()}");
                }
            }
        }
    }
}

