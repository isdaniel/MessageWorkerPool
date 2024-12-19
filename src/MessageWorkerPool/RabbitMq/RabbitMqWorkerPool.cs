using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Threading;
using System.Threading.Tasks;

namespace MessageWorkerPool.RabbitMq
{
    public class RabbitMqWorkerPool : WorkerPoolBase
    {
        private readonly RabbitMqSetting _rabbitMqSetting;
        private IConnection _connection;
        private bool _disposed = false;
        public RabbitMqWorkerPool(RabbitMqSetting rabbitMqSetting, WorkerPoolSetting workerSetting, ILoggerFactory loggerFactory) : base(workerSetting, loggerFactory)
        {

            _rabbitMqSetting = rabbitMqSetting;

            var _connFactory = new ConnectionFactory
            {
                Uri = _rabbitMqSetting.GetUri(),
                DispatchConsumersAsync = true // async mode
            };

            _connection = _connFactory.CreateConnection();
        }

        protected override IWorker GetWorker()
        {
            var channle = _connection.CreateModel();
            var worker = new RabbitMqWorker(_rabbitMqSetting, _workerSetting, channle, _loggerFactory);

            return worker;
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
