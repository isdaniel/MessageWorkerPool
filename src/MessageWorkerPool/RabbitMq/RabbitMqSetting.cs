using System;
using System.Data.Common;
using RabbitMQ.Client;

namespace MessageWorkerPool.RabbitMq
{
    public abstract class MqSettingBase
    {

        public abstract string GetConnectionString();
    }

    public class RabbitMqSetting : MqSettingBase
    {
        /// <summary>
        /// The uri to use for the connection.
        /// </summary>
        /// <returns></returns>
        public Uri GetUri()
        {
            return new Uri(GetConnectionString());
        }

        public string GetUriWithoutPassword()
        {
            return $"amqp://{UserName}:*******@{HostName}:{Port}";
        }

        public override string GetConnectionString()
        {
            return $"amqp://{UserName}:{Password}@{HostName}:{Port}";
        }

        /// <summary>
        /// Rabbit Mq Port
        /// </summary>
        public ushort Port { get; set; }
        public string QueueName { get; set; }
        public string UserName { get; set; }
        /// <summary>
        /// Password to use when authenticating to the server.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// The host to connect to
        /// </summary>
        public string HostName { get; set; }
        /// <summary>
        /// How many task would like to prefetch from message queue
        /// default value is 1 (0 if unlimited)
        /// </summary>
        public ushort PrefetchTaskCount { get; set; } = 1;
        public Func<RabbitMqSetting, IConnection> ConnectionHandler { get; set; } = setting => setting.DefaultConnectionCreator();

        /// <summary>
        /// default creator provide by RabbitMqSetting itself.
        /// </summary>
        /// <returns></returns>
        private IConnection DefaultConnectionCreator (){
            var _connFactory = new ConnectionFactory
            {
                Uri = this.GetUri(),
                DispatchConsumersAsync = true // async mode
            };

            return _connFactory.CreateConnection();
        }
    }
}
