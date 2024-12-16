using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageWorkerPool.RabbitMq
{

    /// <summary>
    /// worker base to handle infrastructure and connection matter, export an Execute method let subclass implement their logic
    /// </summary>
    public abstract class RabbitMqWorkerBase : IWorker
    {
        public RabbitMqSetting Setting { get; }
        protected AsyncEventHandler<BasicDeliverEventArgs> ReceiveEvent;
        private AsyncEventingBasicConsumer _consumer;
        private IConnection _conn;
        private IModel _channle;
        protected ILogger<RabbitMqWorkerBase> Logger { get; }
        public RabbitMqWorkerBase(
            RabbitMqSetting setting,
            ILogger<RabbitMqWorkerBase> logger)
        {
            Logger = logger;
            Setting = setting;

            logger.LogInformation($"RabbitMq connection string: {setting.GetUriWithoutPassword()}");
        }

        /// <summary>
        /// Process incoming messages, sub-class can implement this hock method
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public abstract Task<bool> ExecuteAsync(BasicDeliverEventArgs args, CancellationToken token);

        public virtual async Task InitConnectionAsync(CancellationToken token)
        {
            var _connFactory = new ConnectionFactory
            {
                Uri = Setting.GetUri(),
                DispatchConsumersAsync = true // async mode
            };

            _conn = _connFactory.CreateConnection();
            _channle = _conn.CreateModel();
            _consumer = new AsyncEventingBasicConsumer(_channle);
            _channle.BasicQos(0, Setting.PrefetchTaskCount, true);
            _channle.BasicConsume(Setting.QueueName, false, _consumer);
            ReceiveEvent = async (sender, e) =>
            {
                try
                {
                    var ackReuslt = await ExecuteAsync(e, token);
                    if (ackReuslt)
                        _channle.BasicAck(e.DeliveryTag, false);
                    else
                        _channle.BasicNack(e.DeliveryTag, false, true);
                }
                catch (Exception ex)
                {
                    _channle.BasicNack(e.DeliveryTag, false, true);
                    Logger.LogError(ex, ex.ToString());
                }
                await Task.Yield();
            };
            _consumer.Received += ReceiveEvent;
        }

        //private void BindingEvent(CancellationToken token)
        //{
            
        //}

        /// <summary>
        /// provide hock for sub-class implement 
        /// </summary>
        /// <returns></returns>
        public virtual async Task GracefulReleaseAsync(CancellationToken token)
        {
            await Task.CompletedTask;
        }

        public async Task GracefulShutDownAsync(CancellationToken token)
        {
            _consumer.Received -= ReceiveEvent;
            ReceiveEvent = null;
            //wait for all unit tasks be done.
            Logger.LogInformation("Wait for Pool Close!!!!");

            await GracefulReleaseAsync(token);

            if (_channle.IsOpen)
                _channle.Close();

            if (_conn.IsOpen)
                _conn.Close();

            Logger.LogInformation("RabbitMQ Conn Closed!!!!");
        }
    }
}
