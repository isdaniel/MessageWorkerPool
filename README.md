# MessageWorkerPool

[![License](https://img.shields.io/github/license/isdaniel/MessageWorkerPool)](LICENSE)
[![MessageWorkerPool](https://img.shields.io/nuget/v/MessageWorkerPool.svg?style=plastic)](https://www.nuget.org/packages/MessageWorkerPool/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/MessageWorkerPool.svg)](https://www.nuget.org/packages/MessageWorkerPool/)

## Introduction

`MessageWorkerPool` is a C# library designed to manage a pool of worker threads that process messages concurrently. This helps in efficiently handling tasks in a multi-threaded environment, particularly for applications that require high throughput and low latency.

## Features

- **Thread Pool Management**: Efficiently manages a pool of worker threads.
- **Concurrency**: Supports concurrent processing of messages.
- **NuGet Package**: Easily installable via NuGet.

## Installation

To install the `MessageWorkerPool` package, use the following NuGet command:

```sh
PM > Install-Package MessageWorkerPool
```
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
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        })
        .AddRabbiMqWorkerPool(new RabbitMqSetting
        {
            QueueName = Environment.GetEnvironmentVariable("QUEUENAME"),
            UserName = Environment.GetEnvironmentVariable("USERNAME") ?? "guest",
            Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME"),
            Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort p) ? p : (ushort)5672,
            PrefetchTaskCount = ushort.TryParse(Environment.GetEnvironmentVariable("PREFETCHTASKCOUNT"), out ushort result) ? result : (ushort)1,
            PoolSettings = new PoolSetting[]
            {
                new PoolSetting(){WorkerUnitCount = 3, Group = "groupA", CommandLine = "dotnet", Arguments = @"./ProcessBin/ClientSample.dll"},
                new PoolSetting(){WorkerUnitCount = 3, Group = "groupB", CommandLine = "dotnet", Arguments = @"./ProcessBin/ClientSample.dll"}
            }
        });
```

1. **Scalability**
   - Worker pools can be configured to handle multiple groups (`groupA`, `groupB`), each with its own set of workers.
   - Scaling is achieved by increasing the `WorkerUnitCount` or adding new `PoolSettings` for additional groups.

2. **Decoupling**
   - RabbitMQ acts as a message broker, decoupling the producers of messages from the consumers (workers). This makes it easier to manage workloads independently.

3. **Configurable**
   - The `RabbitMqSetting` object provides flexibility to modify connection settings, queue names, and worker pool details without changing the code.

4. **Reusable Workers**
   - Worker processes are defined by the `CommandLine` and `Arguments`, making it easy to reuse or swap out the tasks performed by the workers.

### Code Architecture

* WorkerPoolFactory.cs: Manages the creation of worker pools based on provided settings.
Throws exceptions if any setting is missing required information.

* WorkerPoolService.cs: Implements a background service to handle the lifecycle and execution of worker pools.
Handles graceful shutdown and cancellation of worker threads.

* PoolSetting.cs: Defines the settings for the worker pools, such as worker unit count, group name, command line, and arguments.
Program Workflow


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
