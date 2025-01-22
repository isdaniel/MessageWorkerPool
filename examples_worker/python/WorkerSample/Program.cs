using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MessageWorkerPool;
using MessageWorkerPool.RabbitMq;
using Microsoft.Extensions.Logging;
using MessageWorkerPool.Extensions;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Console;

public class Program
{
    public static async Task Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole(options =>
                {
                    options.FormatterName = ConsoleFormatterNames.Simple;
                });
                logging.Services.Configure<SimpleConsoleFormatterOptions>(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = " yyyy-MM-dd HH:mm:ss ";
                });
            }).AddRabbitMqWorkerPool(new RabbitMqSetting
            {
                UserName = Environment.GetEnvironmentVariable("USERNAME") ?? "guest",
                Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME"),
                Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort p) ? p : (ushort)5672,
                PrefetchTaskCount = 3
            }, new WorkerPoolSetting() { WorkerUnitCount = 8, CommandLine = "python3", Arguments = @"./processor_py/worker.py", QueueName = Environment.GetEnvironmentVariable("QUEUENAME"), }
            );

}
