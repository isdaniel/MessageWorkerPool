using System;
using MessageWorkerPool.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using MessageWorkerPool.Utilities;

namespace WorkerProcessSample
{
    public class MessageProcessor
    {
        Task _task;
        PipeStreamWrapper _pipeStream;
        CancellationTokenSource closeToken = new CancellationTokenSource();
        volatile int isClose = 0;
        public async Task InitialAsync()
        {
            var pipeName = Console.ReadLine();
            var clientPipe = await PipeClientFactory.CreateAndConnectPipeAsync(pipeName).ConfigureAwait(false);
            _pipeStream = new PipeStreamWrapper(clientPipe);
            _task = Task.Run(async () => {
                Console.WriteLine("in Task.Run...");
                string line;
                while ((line = Console.ReadLine()) != null)
                {
                    if (line == MessageCommunicate.CLOSED_SIGNAL)
                    {
                        closeToken.Cancel();
                        break;
                    }

                }
                Interlocked.Exchange(ref isClose, 1);
            });
        }

        public async Task DoWorkAsync(Func<MessageInputTask, CancellationToken, Task<MessageOutputTask>> process)
        {
            Console.WriteLine("worker starting...");
            Console.WriteLine("Enter text 'quit' to stop:");
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
                        int timeoutMilliseconds = ParseTimeout(task.Headers);
                        var res = await process(task, CreateMessageCancellationToken(timeoutMilliseconds)).ConfigureAwait(false);
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
            Console.WriteLine("loop exits!:");
            await _task;
        }

        int ParseTimeout(IDictionary<string, string> headers)
        {
            if (headers != null &&
                headers.TryGetValue("TimeoutMilliseconds", out var str) &&
                int.TryParse(str, out var timeout))
            {
                return timeout;
            }
            return -1;
        }

        private CancellationToken CreateMessageCancellationToken(int timeoutMilliseconds)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(timeoutMilliseconds < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(timeoutMilliseconds));
            var tokens = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, closeToken.Token);
            return tokens.Token;
        }

        public void DoWork(Func<MessageInputTask, CancellationToken, MessageOutputTask> process)
        {
            Func<MessageInputTask, CancellationToken, Task<MessageOutputTask>> asyncProcess = (task, cancelToken) =>
                Task.FromResult(process(task, cancelToken));

            DoWorkAsync(asyncProcess).GetAwaiter().GetResult();
        }
    }
}

