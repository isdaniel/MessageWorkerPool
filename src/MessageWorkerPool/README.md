# MessageWorkerPool

Efficiently manages a pool of worker processes in C#.

MessageWorkerPool is a C# library that allows you to efficiently manage a pool of worker processes. It integrates with message queue service to handle message processing in a decoupled, scalable, and configurable manner, This helps in efficiently handling tasks in a multi-processes environment, particularly for applications that require high throughput and low latency.


## Program Structure

Here is the sample code for creating and configuring a worker pool that interacts with RabbitMQ. Below is a breakdown of its functionality; The worker pool will fetch message from RabbitMQ server depended on your `RabbitMqSetting` setting and sending the message via `Process.StandardInput` to real worker node that created by users.

```c#
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
            })
            .AddRabbitMqWorkerPool(new RabbitMqSetting
            {
                QueueName = Environment.GetEnvironmentVariable("QUEUENAME"),
                UserName = Environment.GetEnvironmentVariable("USERNAME") ?? "guest",
                Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME"),
                Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort p) ? p : (ushort)5672,
                PrefetchTaskCount = 3
            }, new WorkerPoolSetting() { WorkerUnitCount = 8, CommandLine = "python3", Arguments = @"./worker.py" });
}
```
