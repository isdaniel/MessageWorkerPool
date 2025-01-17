using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MessageWorkerPool;
using MessageWorkerPool.RabbitMq;
using Microsoft.Extensions.Logging;
using MessageWorkerPool.Extensions;
using System;
using System.Security.Cryptography;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.CompilerServices;
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
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "localhost",
                Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort p) ? p : (ushort)5672,
                PrefetchTaskCount = 3
            }, new WorkerPoolSetting[]
            {
                new WorkerPoolSetting() {
                    WorkerUnitCount = 1,
                    CommandLine = "dotnet",
                    Arguments = @"./BalanceWorkerApp/WorkerProcessor.dll" ,
                    QueueName = Environment.GetEnvironmentVariable("BALANCEWORKER_QUEUE")
                },
                new WorkerPoolSetting() {
                    WorkerUnitCount = 3,
                    CommandLine = "dotnet",
                    //Arguments = @"C:\gitRepo\MessageWorkerPool\src\tests\integrationTest\trade_worker\WorkerProcess\FibonacciWorkerApp\RPC_FibonacciWorker.dll",
                    Arguments = @"./FibonacciWorkerApp/RPC_FibonacciWorker.dll" ,
                    QueueName = Environment.GetEnvironmentVariable("FIBONACCI_QUEUE") ?? "integrationTesting_fibonacciQ"
                },
                new WorkerPoolSetting() {
                    WorkerUnitCount = 3,
                    CommandLine = "dotnet",
                    Arguments = @"./LongRunningTaskWorkerApp/LongRunningTaskWorker.dll",
                    QueueName = Environment.GetEnvironmentVariable("LONGRUNNINGBATCHTASK_QUEUE")
                },
            });

}
