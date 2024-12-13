using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MessageWorkerPool.RabbitMq;
using System;
using System.Reflection;

namespace MessageWorkerPool.Extensions
{
    public static class MessageWorkerPoolExtension
    {
        public static IServiceCollection AddRabbiMqWorkerPool(this IServiceCollection services, RabbitMqSetting rabbitMqSetting)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (rabbitMqSetting == null)
                throw new ArgumentNullException(nameof(rabbitMqSetting));

            services.AddHostedService<WorkerPoolService>();
            services.TryAddSingleton<IWorker, RabbitMqGroupWorker>();
            services.TryAddSingleton<IPoolFactory, WorkerPoolFactory>();
            services.AddSingleton(rabbitMqSetting);

            return services;
        }
    }
}
