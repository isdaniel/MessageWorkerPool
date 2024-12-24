using System;
using System.Collections.Generic;
using MessageWorkerPool.RabbitMq;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool
{
    public interface IWorkerPoolFactory
    {
        IWorkerPool CreateWorkerPool(WorkerPoolSetting poolSetting);
    }

    public class WorkerPoolFactory : IWorkerPoolFactory
    {
        private readonly MqSettingBase _mqSetting;
        private readonly ILoggerFactory _loggerFactory;
        private readonly Dictionary<Type, Func<MqSettingBase, WorkerPoolSetting, ILoggerFactory, IWorkerPool>> _registry;

        public WorkerPoolFactory(MqSettingBase mqSetting, ILoggerFactory loggerFactory)
        {
            _mqSetting = mqSetting ?? throw new ArgumentNullException(nameof(mqSetting));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

            _registry = new Dictionary<Type, Func<MqSettingBase, WorkerPoolSetting, ILoggerFactory, IWorkerPool>>
            {
                { typeof(RabbitMqSetting), (mq, pool, logger) => new RabbitMqWorkerPool((RabbitMqSetting)mq, pool, logger) }
                // Add other message queue implementations here.
            };
        }

        public IWorkerPool CreateWorkerPool(WorkerPoolSetting poolSetting)
        {
            if (_registry.TryGetValue(_mqSetting.GetType(), out var factoryFunc))
            {
                return factoryFunc(_mqSetting, poolSetting, _loggerFactory);
            }

            throw new NotSupportedException($"No worker pool factory registered for type {_mqSetting.GetType().Name}");
        }
    }

}
