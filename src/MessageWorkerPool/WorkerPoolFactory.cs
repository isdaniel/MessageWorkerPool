using System;
using System.Collections.Generic;
using MessageWorkerPool.RabbitMq;
using MessageWorkerPool.Utilities;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool
{
    /// <summary>
    /// Interface defining a factory for creating worker pools.
    /// </summary>
    public interface IWorkerPoolFactory
    {
        /// <summary>
        /// Creates a worker pool based on the provided pool settings.
        /// </summary>
        /// <param name="poolSetting">The settings used to configure the worker pool.</param>
        /// <returns>An instance of <see cref="IWorkerPool"/>.</returns>
        IWorkerPool CreateWorkerPool(WorkerPoolSetting poolSetting);
    }
	
	/// <summary>
    /// Factory class for creating instances of worker pools.
    /// Supports multiple types of message queue implementations (e.g., RabbitMQ, Kafka).
    /// </summary>
    public class WorkerPoolFactory : IWorkerPoolFactory
    {
        private readonly MqSettingBase _mqSetting;
        private readonly ILoggerFactory _loggerFactory;
		
		/// <summary>
        /// Registry mapping message queue types to their corresponding worker pool creation functions.
        /// </summary>
        private readonly Dictionary<Type, Func<MqSettingBase, WorkerPoolSetting, ILoggerFactory, IWorkerPool>> _registry;

		/// <summary>
        /// Initializes a new instance of the <see cref="WorkerPoolFactory"/> class.
        /// </summary>
        /// <param name="mqSetting">The message queue settings.</param>
        /// <param name="loggerFactory">The logger factory used to create loggers for the worker pool.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="mqSetting"/> or <paramref name="loggerFactory"/> is null.
        /// </exception>
        public WorkerPoolFactory(MqSettingBase mqSetting, ILoggerFactory loggerFactory)
        {
            _mqSetting = mqSetting ?? throw new ArgumentNullException(nameof(mqSetting));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _registry = new Dictionary<Type, Func<MqSettingBase, WorkerPoolSetting, ILoggerFactory, IWorkerPool>>();
            RegisterGeneric<RabbitMqSetting, RabbitMqWorkerPool>();
        }


        /// <summary>
        /// Registers a worker pool factory for a generic message queue type.
        /// </summary>
        public void RegisterGeneric<TMqSetting, TWorkerPool>()
            where TMqSetting : MqSettingBase
            where TWorkerPool : IWorkerPool
        {
            _registry[typeof(TMqSetting)] = (mq, pool, logger) =>
                (IWorkerPool)Activator.CreateInstance(typeof(TWorkerPool), mq, pool, logger);
        }

        /// <summary>
        /// Creates a worker pool based on the current message queue settings and provided pool settings.
        /// </summary>
        /// <param name="poolSetting">The settings used to configure the worker pool.</param>
        /// <returns>An instance of <see cref="IWorkerPool"/>.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when no factory is registered for the type of message queue settings provided.
        /// </exception>
        public IWorkerPool CreateWorkerPool(WorkerPoolSetting poolSetting)
        {
            Type settingType = _mqSetting.GetType();

            if (_registry.TryGetValue(settingType, out var factoryFunc))
            {
                return factoryFunc(_mqSetting, poolSetting, _loggerFactory);
            }

            if (settingType.IsGenericType)
            {
                Type genericDefinition = settingType.GetGenericTypeDefinition();
                if (_registry.TryGetValue(genericDefinition, out var genericFactory))
                {
                    return genericFactory(_mqSetting, poolSetting, _loggerFactory);
                }
            }

            throw new NotSupportedException($"No worker pool factory registered for type {_mqSetting.GetType().Name}");
        }
    }

}
