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
        static void Main(string[] args)
        {
            MessageProcessor processor = new MessageProcessor();
            processor.DoWork((task) => {
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

