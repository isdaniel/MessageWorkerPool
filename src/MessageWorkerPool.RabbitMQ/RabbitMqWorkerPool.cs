using Microsoft.Extensions.Logging;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.Telemetry.Abstractions;
using RabbitMQ.Client;
using System;


namespace MessageWorkerPool.RabbitMQ
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
        /// <param name="telemetryManager">The telemetry manager for tracking pool operations.</param>
        public RabbitMqWorkerPool(
            RabbitMqSetting rabbitMqSetting,
            WorkerPoolSetting workerSetting,
            IConnection connection,
            ILoggerFactory loggerFactory,
            ITelemetryManager telemetryManager = null)
            : base(workerSetting, loggerFactory, telemetryManager)
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
        /// <param name="telemetryManager">The telemetry manager for tracking pool operations.</param>
        public RabbitMqWorkerPool(
            RabbitMqSetting rabbitMqSetting,
            WorkerPoolSetting workerSetting,
            ILoggerFactory loggerFactory,
            ITelemetryManager telemetryManager = null)
            : this(
                rabbitMqSetting ?? throw new ArgumentNullException(nameof(rabbitMqSetting)),
                workerSetting,
                CreateConnection(rabbitMqSetting, loggerFactory),
                loggerFactory,
                telemetryManager)
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
            return new RabbitMqWorker(_rabbitMqSetting, _workerSetting, channel, logger, _telemetryManager);
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
