﻿using Microsoft.Extensions.DependencyInjection;
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
        public static IHostBuilder AddRabbiMqWorkerPool(this IHostBuilder hostBuilder, RabbitMqSetting rabbitMqSetting)
        {
            if (hostBuilder == null)
                throw new ArgumentNullException(nameof(hostBuilder));


            return hostBuilder.ConfigureServices((context, services) =>
            {
                services.AddHostedService<WorkerPoolService>();
                services.TryAddSingleton<IWorker, RabbitMqGroupWorker>();
                services.TryAddSingleton<IPoolFactory, WorkerPoolFactory>();
                services.AddSingleton(rabbitMqSetting);
            });
        }
    }
}
