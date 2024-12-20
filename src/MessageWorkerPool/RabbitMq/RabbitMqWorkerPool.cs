using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;


namespace MessageWorkerPool.RabbitMq
{
    public class RabbitMqWorkerPool : WorkerPoolBase
    {
        private readonly RabbitMqSetting _rabbitMqSetting;
        private IConnection _connection;
        private bool _disposed = false;

        internal IConnection Connection => _connection;

        /// <summary>
        /// Create RabbitMqWorkerPool.
        /// </summary>
        /// <param name="rabbitMqSetting">RabbitMQ Setting</param>
        /// <param name="workerSetting">>Worker Pool Seeting</param>
        /// <param name="connection">RabitMqconnection</param>
        /// <param name="loggerFactory"></param>
        public RabbitMqWorkerPool(RabbitMqSetting rabbitMqSetting, WorkerPoolSetting workerSetting, IConnection connection, ILoggerFactory loggerFactory) : base(workerSetting, loggerFactory)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            _rabbitMqSetting = rabbitMqSetting;
            _connection = connection;
        }

        /// <summary>
        /// Create RabbitMqWorkerPool.
        /// </summary>
        /// <param name="rabbitMqSetting">RabbitMQ Setting</param>
        /// <param name="workerSetting">Worker Pool Seeting</param>
        /// <param name="loggerFactory"></param>
        public RabbitMqWorkerPool(RabbitMqSetting rabbitMqSetting, WorkerPoolSetting workerSetting, ILoggerFactory loggerFactory) : this(rabbitMqSetting, workerSetting, CreateConnection(rabbitMqSetting, loggerFactory), loggerFactory)
        {
        }

        private static IConnection CreateConnection(RabbitMqSetting rabbitMqSetting, ILoggerFactory loggerFactory)
        {
            if (rabbitMqSetting == null)
            {
                throw new ArgumentNullException(nameof(rabbitMqSetting));
            }

            var logger = loggerFactory.CreateLogger<RabbitMqWorkerPool>();
            try
            {
                return rabbitMqSetting.ConnectionHandler(rabbitMqSetting);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,"RabbitMQ connection create fail!");
                throw;
            }
            
        }

        protected override IWorker GetWorker()
        {
            var channle = _connection.CreateModel();
            return new RabbitMqWorker(_rabbitMqSetting, _workerSetting, channle, _loggerFactory);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            base.Dispose(disposing);

            if (_connection?.IsOpen != null)
            {
                _connection.Close();
                _connection = null;
            }

            _disposed = true;
        }
    }
}
