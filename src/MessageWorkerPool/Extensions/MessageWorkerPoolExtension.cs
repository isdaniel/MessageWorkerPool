using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MessageWorkerPool.RabbitMq;
using System;
using System.Linq;
using MessageWorkerPool.Utilities;
using MessageWorkerPool.KafkaMq;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool.Extensions
{

    public static class MessageWorkerPoolExtension
    {
        public static IHostBuilder AddRabbitMqWorkerPool(this IHostBuilder hostBuilder, RabbitMqSetting rabbitMqSetting, WorkerPoolSetting[] workerSettings)
        {
            if (hostBuilder == null)
                throw new ArgumentNullException(nameof(hostBuilder));

            return hostBuilder.ConfigureServices((service) =>
            {
                AddRabbitMqWorkerPool(service, rabbitMqSetting, workerSettings);
            });
        }

        public static IServiceCollection AddRabbitMqWorkerPool(this IServiceCollection services, RabbitMqSetting rabbitMqSetting, WorkerPoolSetting[] workerSettings)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (rabbitMqSetting == null)
                throw new ArgumentNullException(nameof(rabbitMqSetting));

            if (workerSettings == null)
                throw new ArgumentNullException(nameof(workerSettings));

            if (workerSettings.Any(x => x == null))
                throw new InvalidOperationException("workerSettings contains null setting.");

            services.AddSingleton<MqSettingBase, RabbitMqSetting>(provider =>
            {
                return rabbitMqSetting;
            });
            services.AddHostedService<WorkerPoolService>();
            services.AddSingleton(workerSettings);
            services.AddSingleton<IWorkerPoolFactory, WorkerPoolFactory>();

            return services;
        }

        public static IServiceCollection AddRabbitMqWorkerPool(this IServiceCollection services, RabbitMqSetting rabbitMqSetting, WorkerPoolSetting workerSettings)
        {
            return AddRabbitMqWorkerPool(services, rabbitMqSetting, new WorkerPoolSetting[] { workerSettings});
        }

        public static IHostBuilder AddRabbitMqWorkerPool(this IHostBuilder hostBuilder, RabbitMqSetting rabbitMqSetting, WorkerPoolSetting workerSettings)
        {
            return AddRabbitMqWorkerPool(hostBuilder, rabbitMqSetting, new WorkerPoolSetting[] { workerSettings});
        }

        public static IServiceCollection AddKafkaMqWorkerPool<TKey>(this IServiceCollection services, KafkaSetting<TKey> kafkaSetting, WorkerPoolSetting[] workerSettings)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (kafkaSetting == null)
                throw new ArgumentNullException(nameof(kafkaSetting));

            if (workerSettings == null)
                throw new ArgumentNullException(nameof(workerSettings));

            if (workerSettings.Any(x => x == null))
                throw new InvalidOperationException("workerSettings contains null setting.");

            services.AddSingleton<MqSettingBase, KafkaSetting<TKey>>(provider =>
            {
                return kafkaSetting;
            });
            
            services.AddHostedService<WorkerPoolService>();
            services.AddSingleton(workerSettings);
            services.AddSingleton<IWorkerPoolFactory, WorkerPoolFactory>(provider =>
            {
                var setting = provider.GetService<MqSettingBase>();
                var loggerFactory = provider.GetService<ILoggerFactory>();
                var factory = new WorkerPoolFactory(setting, loggerFactory);
                factory.RegisterGeneric<KafkaSetting<TKey>, KafkaMqWorkerPool<TKey>>();
                return factory;
            });

            return services;
        }

        public static IHostBuilder AddKafkaMqWorkerPool<TKey>(this IHostBuilder hostBuilder, KafkaSetting<TKey> kafkaSetting, WorkerPoolSetting[] workerSettings)
        {
            if (hostBuilder == null)
                throw new ArgumentNullException(nameof(hostBuilder));

            return hostBuilder.ConfigureServices((service) =>
            {
                AddKafkaMqWorkerPool(service, kafkaSetting, workerSettings);
            });
        }

    }
}
