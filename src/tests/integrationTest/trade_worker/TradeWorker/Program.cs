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
using Microsoft.Extensions.Logging.Abstractions;
using MessageWorkerPool.KafkaMq;
using Confluent.Kafka;

public static class EnvironmentVAR
{
    public static readonly string HOSTNAME = Environment.GetEnvironmentVariable("KAFKA_HOSTNAME") ?? "localhost:9092";
    public static readonly string GROUPID = Environment.GetEnvironmentVariable("KAFKA_GROUPID") ?? "integrationGroup";
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var broker = Environment.GetEnvironmentVariable("BROKER_TYPE") ?? "None";
        var builder = CreateHostBuilder(args);

        switch (broker.ToLower())
        {
            case "rabbitmq":
                AddRabbitMqWorkerPool(builder);
                break;
            case "kafka":
                AddKafkaWorkerPool(builder);
                break;
        }

        builder.Build().Run();
    }

    private static void AddKafkaWorkerPool(IHostBuilder builder)
    {
        builder.AddKafkaMqWorkerPool(new KafkaSetting<Null>()
        {
            ProducerCfg = new ProducerConfig()
            {
                BootstrapServers = EnvironmentVAR.HOSTNAME,
                Acks = Acks.All,
                EnableIdempotence = true,
                // Performance Tuning
                BatchSize = 32 * 1024
            },
            ConsumerCfg = new ConsumerConfig()
            {
                BootstrapServers = EnvironmentVAR.HOSTNAME,
                GroupId = EnvironmentVAR.GROUPID,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            },
        }, new WorkerPoolSetting[]{
                    new WorkerPoolSetting()
                    {
                        WorkerUnitCount = 1,
                        CommandLine = "dotnet",
                        Arguments = @"./BalanceWorkerApp/WorkerProcessor.dll" ,
                        QueueName = Environment.GetEnvironmentVariable("BALANCEWORKER_QUEUE")
                    },
                    new WorkerPoolSetting()
                    {
                        WorkerUnitCount = 4,
                        CommandLine = "dotnet",
                        Arguments = @"./FibonacciWorkerApp/RPC_FibonacciWorker.dll" ,
                        QueueName = Environment.GetEnvironmentVariable("FIBONACCI_QUEUE") ?? "integrationTesting_fibonacciQ"
                    },
                    new WorkerPoolSetting()
                    {
                        WorkerUnitCount = 4,
                        CommandLine = "dotnet",
                        Arguments = @"./LongRunningTaskWorkerApp/LongRunningTaskWorker.dll",
                        QueueName = Environment.GetEnvironmentVariable("LONGRUNNINGBATCHTASK_QUEUE")
                    },
                });
    }

    private static void AddRabbitMqWorkerPool(IHostBuilder builder)
    {
        builder.AddRabbitMqWorkerPool(new RabbitMqSetting
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
            });

}
