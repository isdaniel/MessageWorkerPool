using Microsoft.Extensions.DependencyInjection;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.RabbitMq;
using FluentAssertions;
using Microsoft.Extensions.Hosting;

namespace MessageWorkerPool.Test
{
    public class MessageWorkerPoolExtensionTest
    {
        [Fact]
        public void AddRabbitMqWorkerPool_ShouldRegisterRabbitMqSettingInServiceProvider()
        {
            var rabbitMQSetting = new RabbitMqSetting(){
                HostName = "localhost",
                Password = "abcd1234",
                Port = 5672,
                QueueName = "queue1",
                UserName = "user1",
                PrefetchTaskCount = 1,
                PoolSettings = new PoolSetting []{
                    new PoolSetting(){
                        Arguments = "client.dll",
                        Group = "groupA",
                        WorkUnitCount = 1,
                        CommnadLine = "dotnet"
                    }
                }
            };

            var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(); // Add logging
                    services.AddRabbiMqWorkerPool(rabbitMQSetting); // Register the worker pool
                }).Build();

            var serviceProvider = host.Services;

            var expectSetting = serviceProvider.GetService<RabbitMqSetting>();

            expectSetting.Should().NotBeNull();
            expectSetting.Should().Be(rabbitMQSetting);

            var worker = serviceProvider.GetService<IWorker>();
            worker.Should().NotBeNull();
            worker.Should().BeOfType<RabbitMqGroupWorker>();

            var poolFactory = serviceProvider.GetService<IPoolFactory>();
            poolFactory.Should().NotBeNull();
            poolFactory.Should().BeOfType<WorkerPoolFactory>();

            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
        }


        [Fact]
        public void AddRabbitMqWorkerPool_ShouldThrowArgumentNullException_WhenRabbitMqSettingIsNull()
        {
            var services = new ServiceCollection();
            var rabbitMQSetting = default(RabbitMqSetting);

            Action act = () => services.AddRabbiMqWorkerPool(rabbitMQSetting);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AddRabbiMqWorkerPool_ShouldRegisterRabbitMqSettingAndServices()
        {
            // Arrange
            var rabbitMqSetting = new RabbitMqSetting
            {
                HostName = "localhost",
                Password = "abcd1234",
                Port = 5672,
                QueueName = "queue1",
                UserName = "user1",
                PrefetchTaskCount = 1,
                PoolSettings = new PoolSetting[]
                {
                new PoolSetting
                {
                    Arguments = "client.dll",
                    Group = "groupA",
                    WorkUnitCount = 1,
                    CommnadLine = "dotnet"
                }
                }
            };

            var hostBuilder = new HostBuilder();

            // Act
            using var host = hostBuilder.AddRabbiMqWorkerPool(rabbitMqSetting).Build();
            var serviceProvider = host.Services;

            // Assert
            var resolvedSetting = serviceProvider.GetService<RabbitMqSetting>();
            resolvedSetting.Should().NotBeNull();
            resolvedSetting.Should().Be(rabbitMqSetting);

            var worker = serviceProvider.GetService<IWorker>();
            worker.Should().NotBeNull();
            worker.Should().BeOfType<RabbitMqGroupWorker>();

            var poolFactory = serviceProvider.GetService<IPoolFactory>();
            poolFactory.Should().NotBeNull();
            poolFactory.Should().BeOfType<WorkerPoolFactory>();

            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
        }

        [Fact]
        public void AddRabbiMqWorkerPool_ShouldThrowArgumentNullException_WhenHostBuilderIsNull()
        {
            // Arrange
            IHostBuilder hostBuilder = null;
            var rabbitMqSetting = new RabbitMqSetting();

            // Act
            Action act = () => hostBuilder.AddRabbiMqWorkerPool(rabbitMqSetting).Build();

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithMessage("*hostBuilder*");
        }

        [Fact]
        public void AddRabbiMqWorkerPool_ShouldThrowArgumentNullException_WhenRabbitMqSettingIsNull()
        {
            // Arrange
            var hostBuilder = new HostBuilder();
            RabbitMqSetting rabbitMqSetting = null;

            // Act
            Action act = () => hostBuilder.AddRabbiMqWorkerPool(rabbitMqSetting).Build();

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithMessage("*rabbitMqSetting*");
        }
    }
}
