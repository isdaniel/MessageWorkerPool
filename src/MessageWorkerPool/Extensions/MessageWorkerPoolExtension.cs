using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MessageWorkerPool.RabbitMq;
using System;
using System.Linq;

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
            services.AddTransient<WorkerPoolFactory>();
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
    }
}
