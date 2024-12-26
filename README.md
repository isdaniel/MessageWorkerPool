# MessageWorkerPool

[![License](https://img.shields.io/github/license/isdaniel/MessageWorkerPool)](LICENSE)
[![MessageWorkerPool](https://img.shields.io/nuget/v/MessageWorkerPool.svg?style=plastic)](https://www.nuget.org/packages/MessageWorkerPool/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MessageWorkerPool.svg)](https://www.nuget.org/packages/MessageWorkerPool/)
[![Build Action](https://github.com/isdaniel/MessageWorkerPool/actions/workflows/dotnet_build.yml/badge.svg)](https://github.com/isdaniel/MessageWorkerPool/actions/workflows/dotnet_build.yml)
[![Integration Test](https://github.com/isdaniel/MessageWorkerPool/actions/workflows/integration_test.yml/badge.svg)](https://github.com/isdaniel/MessageWorkerPool/actions/workflows/integration_test.yml)

## Introduction

Efficiently manages a pool of worker processes in C#.

MessageWorkerPool is a C# library that allows you to efficiently manage a pool of worker processes. It integrates with message queue service to handle message processing in a decoupled, scalable, and configurable manner, This helps in efficiently handling tasks in a multi-processes environment, particularly for applications that require high throughput and low latency.

## Why Process Pool rather than Thread Pool?

Use a process pool when you need robust isolation to prevent issues in one task from affecting others, especially for critical or crash-prone operations,although thread pool would be more lightweight (as threads share memory and require less context-switching overhead), however Process Pool would provide more flexibility solution by implement different program language.

## Installation

To install the `MessageWorkerPool` package, use the following NuGet command:

```sh
PM > Install-Package MessageWorkerPool
```

To install the library, clone the repository and build the project:

```
git clone https://github.com/isdaniel/MessageWorkerPool.git
cd MessageWorkerPool
dotnet build
```

## Architecture overview

![](./images/arhc-overview.png)

## Quick Start

Here’s a quick start guide for deploying your RabbitMQ and related services using the provided `docker-compose.yml` file and environment variables from `.env`.

```
docker-compose --env-file .\env\.env up --build -d
```

1. Check RabbitMQ health status: Open `http://localhost:8888` in your browser to access the RabbitMQ Management Dashboard.
  * Username: guest
  * Password: guest
2. Check OrleansDashboard `http://localhost:8899`
  * Username: admin
  * Password: test.123

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

1. **Scalability**
   - Scaling is achieved by increasing the `WorkerUnitCount` & `PrefetchTaskCount` determined how many amount of fetching message from rabbitMQ at same time.

2. **Decoupling**
   - RabbitMQ acts as a message broker, decoupling the producers of messages from the consumers (workers). This makes it easier to manage workloads independently.

3. **Configurable**
   - The `RabbitMqSetting` object provides flexibility to modify connection settings, queue names, and worker pool details without changing the code.

4. **Reusable Workers**
   - Worker processes are defined by the `CommandLine` and `Arguments`, making it easy to reuse or swap out the tasks performed by the workers.

## Protocol between worker and task process

The Protocol between worker and task process are use JSON format with standardInput & standardInout.

### Worker Standard Output

Currently, there are some status represnt status

* IGNORE_MESSAGE (-1) : append message to standard output.
* MESSAGE_DONE (200) : tell worker this case can ack from message queue service.
* MESSAGE_DONE_WITH_REPLY (201) [not implemented yet]

Status = -1 via Standard Output, task process tell worker this isn't a response nor ack message, only for record via Standard Output.

```
{"Message":"this is func task.., message Send Time[2024/12/19 06:59:43:646] this message belong with groupA, Sleeping 1s","Status":-1}
```

Status = 200 via Standard Output, task process tell worker the task can be acked that mean it was finished.

```
{"Message":"message done","Status":200}
```

We can write our own worker by different program language (I have provided python and .net sample in this repository).

## Contributing Guidelines

1. **Fork the Repository**: Start by forking the project repository to your github account.
2. **Clone Locally**: Clone the forked repository to your local machine using a git client.
   ```sh
   git clone https://github.com/isdaniel/MessageWorkerPool
   ```
3. **Create a New Branch**: Always work on a new branch, giving it a descriptive name.
   ```sh
   git checkout -b new-feature-x
   ```
4. **Make Your Changes**: Develop and test your changes locally.
5. **Commit Your Changes**: Commit with a clear message describing your updates.
   ```sh
   git commit -m 'Implemented new feature x.'
   ```
6. **Push to github**: Push the changes to your forked repository.
   ```sh
   git push origin new-feature-x
   ```
7. **Submit a Pull Request**: Create a PR against the original project repository. Clearly describe the changes and their motivations.
8. **Review**: Once your PR is reviewed and approved, it will be merged into the main branch. Congratulations on your contribution!
</details>

<details closed>
<summary>Contributor Graph</summary>
<br>
<p align="left">
   <a href="https://github.com{/isdaniel/MessageWorkerPool/}graphs/contributors">
      <img src="https://contrib.rocks/image?repo=isdaniel/MessageWorkerPool">
   </a>
</p>
</details>

---
