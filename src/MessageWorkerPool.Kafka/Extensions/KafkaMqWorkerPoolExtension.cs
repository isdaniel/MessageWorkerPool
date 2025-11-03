using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using MessageWorkerPool.Utilities;
using MessageWorkerPool.Telemetry.Abstractions;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace MessageWorkerPool.Kafka.Extensions
{
    public static class KafkaMqWorkerPoolExtension
    {
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
                var telemetryManager = provider.GetService<ITelemetryManager>();
                var factory = new WorkerPoolFactory(setting, loggerFactory, telemetryManager);
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
