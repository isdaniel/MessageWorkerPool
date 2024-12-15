using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MessageWorkerPool;
using MessageWorkerPool.RabbitMq;
using Microsoft.Extensions.Logging;
using MessageWorkerPool.Extensions;
using System;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }


    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            }).AddRabbiMqWorkerPool(new RabbitMqSetting
            {
                QueueName = Environment.GetEnvironmentVariable("QUEUENAME"),
                UserName = Environment.GetEnvironmentVariable("USERNAME") ?? "guest",
                Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME"),
                Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort p) ? p : (ushort) 5672,
                PrefetchTaskCount = ushort.TryParse(Environment.GetEnvironmentVariable("PREFETCHTASKCOUNT"), out ushort result) ? result : (ushort) 1,
                PoolSettings = new PoolSetting[] //which can read from setting files.
                {
                    new PoolSetting(){WorkerUnitCount = 3,Group = "groupA" , CommnadLine = "dotnet",Arguments = @"./ProcessBin/ClientSamlpe.dll"},
                    new PoolSetting(){WorkerUnitCount = 3,Group = "groupB" , CommnadLine = "dotnet",Arguments = @"./ProcessBin/ClientSamlpe.dll"}
                }
            });

}
