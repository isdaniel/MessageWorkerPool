using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using MessageWorkerPool.IO;
using MessageWorkerPool.Utilities;

namespace ShareLib
{
    public class MessageProcessor
    {
        Task _task;
        PipeStreamWrapper _pipeStream;

        volatile int isClose = 0;
        public async Task InitialAsync() {
            var pipeName = Console.ReadLine();
            var clientPipe = await PipeClientFactory.CreateAndConnectPipeAsync(pipeName);
            _pipeStream = new PipeStreamWrapper(clientPipe);
            _task = Task.Run(async () => {
                Console.WriteLine("in Task.Run...".ToIgnoreMessage());
                string line;
                while ((line = Console.ReadLine()) != null && line != MessageCommunicate.CLOSED_SIGNAL)
                {
                    //todo debug..etc
                }
                Interlocked.Exchange(ref isClose, 1);
            });
        }

        public async Task DoWorkAsync(Func<MessageInputTask, Task<MessageOutputTask>> process)
        {
            Console.WriteLine("worker starting...".ToIgnoreMessage());
            Console.WriteLine("Enter text 'quit' to stop:".ToIgnoreMessage());
            while (Interlocked.CompareExchange(ref isClose, 1, 1) == 0)
            {
                try
                {
                    var task = await _pipeStream.ReadAsync<MessageInputTask>().ConfigureAwait(false);
  
                    if (task == null)
                    {
                        //todo handle...
                    }
                    else
                    {
                        var res = await process(task).ConfigureAwait(false);
                        await _pipeStream.WriteAsync(res).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Worker occur unexpected error: {ex.ToString()}");
 
                    await _pipeStream.WriteAsync(new MessageOutputTask()
                    {
                        Message = ex.Message,
                        Status = MessageStatus.MESSAGE_DONE
                    }.ToJson()).ConfigureAwait(false);
                }
            }

            _pipeStream.Dispose();
            Console.WriteLine("loop exits!:".ToIgnoreMessage());
            await _task;
        }

        public void DoWork(Func<MessageInputTask, MessageOutputTask> process)
        {
            Func<MessageInputTask, Task<MessageOutputTask>> asyncProcess = task =>
                Task.FromResult(process(task));

            DoWorkAsync(asyncProcess).GetAwaiter().GetResult();
        }
    }
}

