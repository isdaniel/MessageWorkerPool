using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.Utilities;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool.KafkaMq
{
    public class KafkaMqWorker<TKey> : WorkerBase
    {
        private readonly KafkaSetting<TKey> _kafkaSetting;
        private bool _disposed = false;
        private IConsumer<TKey, string> _consumer;
        private IProducer<TKey, string> _producer;
        private Task _consumptionTask;
        private readonly CancellationTokenSource _cancellationTokenSource;
        //int _messageCount = 0;
        public KafkaMqWorker(WorkerPoolSetting workerSetting, KafkaSetting<TKey> kafkaSetting, ILogger logger) : base(workerSetting, logger)
        {
            _kafkaSetting = kafkaSetting;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        protected override void SetupMessageQueueSetting(CancellationToken token)
        {
            _consumer = _kafkaSetting.GetConsumer();
            _producer = _kafkaSetting.GetProducer();
            _consumer.Subscribe(_workerSetting.QueueName);
            var taskToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, token).Token;
            _consumptionTask = Task.Run(async () => await StartMessageConsumptionLoop(taskToken));
        }

        private async Task StartMessageConsumptionLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !_stoppingStatus.Contains(this.Status))
            {
                try
                {
                    var result = _consumer.Consume(token);
                    await HandleMessageAsync(result).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Unexpected error in message loop");
                }
            }
        }

        private async Task HandleMessageAsync(ConsumeResult<TKey, string> result)
        {
            var headers = result.Message.Headers.ToDictionary(x => x.Key, x => Encoding.UTF8.GetString(x.GetValueBytes()));

            Logger.LogDebug($"received message:{result.Message.Value}");

            await DataStreamWriteAsync(new MessageInputTask
            {
                Message = result.Message.Value,
                CorrelationId = headers.TryGetValueOrDefault("CorrelationId"),
                Headers = headers,
                OriginalQueueName = _workerSetting.QueueName,
            });

            var taskOutput = await DataStreamReadAsync<MessageOutputTask>();

            if (_messageDoneMap.Contains(taskOutput.Status))
            {
                await HandleSuccessfulMessage(result, headers, taskOutput);
            }
        }

        private async Task HandleSuccessfulMessage(ConsumeResult<TKey, string> result, Dictionary<string, string> headers, MessageOutputTask taskOutput)
        {
            string replyQueue = !string.IsNullOrWhiteSpace(taskOutput.ReplyQueueName) ? taskOutput.ReplyQueueName : headers.TryGetValueOrDefault("ReplyTo");
            await ReplyQueueAsync(replyQueue, taskOutput, (Func<Task>)(async () =>
            {
                var replyHeaders = new Headers();
                foreach (var item in headers)
                {
                    replyHeaders.Add(item.Key, Encoding.UTF8.GetBytes(item.Value));
                }

                await _producer.ProduceAsync(replyQueue, new Message<TKey, string>()
                {
                    Headers = replyHeaders,
                    Value = taskOutput.Message
                });
            }));
            _consumer.Commit(result);
        }

        protected override async Task GracefulReleaseAsync(CancellationToken token)
        {
            _cancellationTokenSource.Cancel();
            if (_consumptionTask != null)
            {
                await _consumptionTask;
            }

            await base.GracefulReleaseAsync(token);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_consumer != null)
                {
                    _consumer.Close();
                    _consumer.Dispose();
                    _consumer = null;
                }

                if (_producer != null)
                {
                    _producer.Flush();
                    _producer.Dispose();
                    _producer = null;
                }
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
