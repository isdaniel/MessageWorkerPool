using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MessageWorkerPool.Utilities;

namespace WorkerProcessSample
{

    class Program
    {
        static async Task Main(string[] args)
        {
            MessageProcessor processor = new MessageProcessor();
            await processor.InitialAsync();
            processor.DoWork((task,token) => {
                Console.WriteLine($"this is func task.., message {task.Message}, Sleeping 1s".ToIgnoreMessage());
                Thread.Sleep(1000);
                return new MessageOutputTask()
                {
                    Message = "New OutPut Message!",
                    Status = MessageStatus.MESSAGE_DONE
                };
            });
        }
    }
}

