using MessageWorkerPool.RabbitMq;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool
{
    public class WorkerPoolFacorty {
        private readonly MqSettingBase _mqSetting;
        private readonly ILoggerFactory _loggerFactory;

        public WorkerPoolFacorty(MqSettingBase mqSetting, ILoggerFactory loggerFactory)
        {
            _mqSetting = mqSetting;
            _loggerFactory = loggerFactory;
        }

        public IWorkerPool GetWorkPoolInstacne(WorkerPoolSetting poolSetting) {
            if (_mqSetting is RabbitMqSetting rabbitMqSetting)
            {
                return new RabbitMqWorkerPool(rabbitMqSetting, poolSetting, _loggerFactory);
            }

            //support other message queue.
            return null;
        }
    }
}
