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
        public void AddRabbitMqWorkerPool_ShouldResolveRegisteredServicesAndSettings()
        {
            var rabbitMQSetting = new RabbitMqSetting()
            {
                HostName = "localhost",
                Password = "abcd1234",
                Port = 5672,
                QueueName = "queue1",
                UserName = "user1",
                PrefetchTaskCount = 1
            };

            var workPoolSetting = new WorkerPoolSetting() { 
                Arguments = "dummy_Arguments",
                CommnadLine = "dummy_CommnadLine",
                WorkerUnitCount = 5,

            };

            var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(); // Add logging
                    services.AddRabbitMqWorkerPool(rabbitMQSetting, workPoolSetting); // Register the worker pool
                }).Build();

            var serviceProvider = host.Services;

            var expectSetting = serviceProvider.GetService<MqSettingBase>();
            var workerSetting = serviceProvider.GetService<WorkerPoolSetting[]>();

            workerSetting.Should().NotBeNull();
            workerSetting.Length.Should().Be(1); // Assuming one setting is registered
            workerSetting[0].Arguments.Should().Be("dummy_Arguments");
            workerSetting[0].CommnadLine.Should().Be("dummy_CommnadLine");
            workerSetting[0].WorkerUnitCount.Should().Be(5);
            workerSetting.Should().NotBeNull();

            expectSetting.Should().NotBeNull();
            expectSetting.Should().Be(rabbitMQSetting);

            var workerFactory = serviceProvider.GetService<WorkerPoolFacorty>();
            workerFactory.Should().NotBeNull();
            workerFactory.Should().BeOfType<WorkerPoolFacorty>();

            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
        }


        [Fact]
        public void AddRabbitMqWorkerPool_ShouldThrowArgumentNullException_WhenBothSettingsAreNull()
        {
            var services = new ServiceCollection();
            var rabbitMQSetting = default(RabbitMqSetting);

            Action act = () => services.AddRabbitMqWorkerPool(null, default(WorkerPoolSetting));
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AddRabbitMqWorkerPool_ShouldThrowArgumentNullException_WhenRabbitMqSettingIsNull()
        {
            var services = new ServiceCollection();
            var rabbitMQSetting = default(RabbitMqSetting);

            Action act = () => services.AddRabbitMqWorkerPool(rabbitMQSetting, default(WorkerPoolSetting));
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AddRabbitMqWorkerPool_ShouldThrowArgumentNullException_WhenHostBuilderIsNull()
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
            };

            var hostBuilder = new HostBuilder();

            // Act
            using var host = hostBuilder.AddRabbitMqWorkerPool(rabbitMqSetting, new WorkerPoolSetting() {
                Arguments = "dummy_Arguments",
                CommnadLine = "dummy_CommnadLine",
                WorkerUnitCount = 5,
            }).Build();
            var serviceProvider = host.Services;

            // Assert
            var resolvedSetting = serviceProvider.GetService<MqSettingBase>();
            resolvedSetting.Should().NotBeNull();
            resolvedSetting.Should().Be(rabbitMqSetting);

            var poolFactory = serviceProvider.GetService<WorkerPoolFacorty>();
            poolFactory.Should().NotBeNull();
            poolFactory.Should().BeOfType<WorkerPoolFacorty>();

            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
        }

        [Fact]
        public void AddRabbitMqWorkerPool_ShouldThrowArgumentNullException_WhenRabbitMqSettingIsNullInHostBuilder()
        {
            // Arrange
            IHostBuilder hostBuilder = null;
            var rabbitMqSetting = new RabbitMqSetting();

            // Act
            Action act = () => hostBuilder.AddRabbitMqWorkerPool(rabbitMqSetting, new WorkerPoolSetting()).Build();

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
            Action act = () => hostBuilder.AddRabbitMqWorkerPool(rabbitMqSetting, new WorkerPoolSetting()).Build();

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithMessage("*rabbitMqSetting*");
        }
    }
}
