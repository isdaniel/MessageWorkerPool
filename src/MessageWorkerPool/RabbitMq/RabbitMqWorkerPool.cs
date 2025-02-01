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
        /// <param name="workerSetting">Worker Pool Setting</param>
        /// <param name="connection">RabbitMQ connection</param>
        /// <param name="loggerFactory"></param>
        public RabbitMqWorkerPool(
            RabbitMqSetting rabbitMqSetting,
            WorkerPoolSetting workerSetting,
            IConnection connection,
            ILoggerFactory loggerFactory)
            : base(workerSetting, loggerFactory)
        {
            _rabbitMqSetting = rabbitMqSetting ?? throw new ArgumentNullException(nameof(rabbitMqSetting));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// Create RabbitMqWorkerPool.
        /// </summary>
        /// <param name="rabbitMqSetting">RabbitMQ Setting</param>
        /// <param name="workerSetting">Worker Pool Setting</param>
        /// <param name="loggerFactory"></param>
        public RabbitMqWorkerPool(
            RabbitMqSetting rabbitMqSetting,
            WorkerPoolSetting workerSetting,
            ILoggerFactory loggerFactory)
            : this(
                rabbitMqSetting ?? throw new ArgumentNullException(nameof(rabbitMqSetting)),
                workerSetting,
                CreateConnection(rabbitMqSetting, loggerFactory),
                loggerFactory)
        {
        }

        internal static IConnection CreateConnection(RabbitMqSetting rabbitMqSetting, ILoggerFactory loggerFactory)
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
                logger.LogError(ex, $"Failed to create RabbitMQ connection for host: {rabbitMqSetting?.HostName}");
                throw;
            }
            
        }

        protected override IWorker GetWorker()
        {
            var channel = _connection.CreateModel();
            var logger = _loggerFactory.CreateLogger<RabbitMqWorker>();
            return new RabbitMqWorker(_rabbitMqSetting, _workerSetting, channel, logger);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            base.Dispose(disposing);

            if (_connection?.IsOpen == true)
            {
                _connection.Close();
            }

            _disposed = true;
        }
    }
}
