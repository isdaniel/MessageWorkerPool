using Microsoft.Extensions.DependencyInjection;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.RabbitMq;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using MessageWorkerPool.Utilities;
using MessageWorkerPool.KafkaMq;
using FluentAssertions.Common;

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
                UserName = "user1",
                PrefetchTaskCount = 1
            };

            var workPoolSetting = new WorkerPoolSetting()
            {
                Arguments = "dummy_Arguments",
                CommandLine = "dummy_CommandLine",
                WorkerUnitCount = 5,
                QueueName = "queue1",
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
            workerSetting[0].CommandLine.Should().Be("dummy_CommandLine");
            workerSetting[0].WorkerUnitCount.Should().Be(5);
            workerSetting.Should().NotBeNull();

            expectSetting.Should().NotBeNull();
            expectSetting.Should().Be(rabbitMQSetting);

            var workerFactory = serviceProvider.GetService<IWorkerPoolFactory>();
            workerFactory.Should().NotBeNull();
            workerFactory.Should().BeOfType<WorkerPoolFactory>();

            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
        }


        [Fact]
        public void AddRabbitMqWorkerPool_ShouldResolveRegisteredServicesAndTwoSettings()
        {
            var rabbitMQSetting = new RabbitMqSetting()
            {
                HostName = "localhost",
                Password = "abcd1234",
                Port = 5672,
                UserName = "user1",
                PrefetchTaskCount = 1
            };

            var workPoolSetting1 = new WorkerPoolSetting()
            {
                Arguments = "dummy_Arguments",
                CommandLine = "dummy_CommandLine",
                WorkerUnitCount = 5,
                QueueName = "queue1"
            };

            var workPoolSetting2 = new WorkerPoolSetting()
            {
                Arguments = "dummy_pyargs",
                CommandLine = "dummy_py",
                WorkerUnitCount = 3,

            };

            var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(); // Add logging
                    services.AddRabbitMqWorkerPool(rabbitMQSetting, new WorkerPoolSetting[] {
                        workPoolSetting1,
                        workPoolSetting2
                    }); // Register the worker pool
                }).Build();

            var serviceProvider = host.Services;

            var expectSetting = serviceProvider.GetService<MqSettingBase>();
            var workerSetting = serviceProvider.GetService<WorkerPoolSetting[]>();

            workerSetting.Should().NotBeNull();
            workerSetting.Length.Should().Be(2); // Assuming one setting is registered
            workerSetting[0].Arguments.Should().Be("dummy_Arguments");
            workerSetting[0].CommandLine.Should().Be("dummy_CommandLine");
            workerSetting[0].WorkerUnitCount.Should().Be(5);

            workerSetting[1].Arguments.Should().Be("dummy_pyargs");
            workerSetting[1].CommandLine.Should().Be("dummy_py");
            workerSetting[1].WorkerUnitCount.Should().Be(3);
            workerSetting.Should().NotBeNull();


            expectSetting.Should().NotBeNull();
            expectSetting.Should().Be(rabbitMQSetting);

            var workerFactory = serviceProvider.GetService<IWorkerPoolFactory>();
            workerFactory.Should().NotBeNull();
            workerFactory.Should().BeOfType<WorkerPoolFactory>();

            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
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
        public void AddRabbitMqWorkerPool_By_IHostBuilder_ShouldResolveRegisteredServicesAndTwoSettings()
        {
            // Arrange
            var rabbitMqSetting = new RabbitMqSetting
            {
                HostName = "localhost",
                Password = "abcd1234",
                Port = 5672,
                UserName = "user1",
                PrefetchTaskCount = 1,
            };

            var hostBuilder = new HostBuilder();

            var workPoolSetting1 = new WorkerPoolSetting()
            {
                Arguments = "dummy_Arguments",
                CommandLine = "dummy_CommandLine",
                WorkerUnitCount = 5,
                QueueName = "queue1",
            };

            var workPoolSetting2 = new WorkerPoolSetting()
            {
                Arguments = "dummy_pyargs",
                CommandLine = "dummy_py",
                WorkerUnitCount = 3,

            };

            // Act
            using var host = hostBuilder.AddRabbitMqWorkerPool(rabbitMqSetting, new WorkerPoolSetting[] {
                        workPoolSetting1,
                        workPoolSetting2
                    }).Build();
            var serviceProvider = host.Services;

            // Assert
            var workerSetting = serviceProvider.GetService<WorkerPoolSetting[]>();

            workerSetting.Should().NotBeNull();
            workerSetting.Length.Should().Be(2); // Assuming one setting is registered
            workerSetting[0].Arguments.Should().Be("dummy_Arguments");
            workerSetting[0].CommandLine.Should().Be("dummy_CommandLine");
            workerSetting[0].WorkerUnitCount.Should().Be(5);

            workerSetting[1].Arguments.Should().Be("dummy_pyargs");
            workerSetting[1].CommandLine.Should().Be("dummy_py");
            workerSetting[1].WorkerUnitCount.Should().Be(3);
            workerSetting.Should().NotBeNull();

            var resolvedSetting = serviceProvider.GetService<MqSettingBase>();
            resolvedSetting.Should().NotBeNull();
            resolvedSetting.Should().Be(rabbitMqSetting);

            var poolFactory = serviceProvider.GetService<IWorkerPoolFactory>();
            poolFactory.Should().NotBeNull();
            poolFactory.Should().BeOfType<WorkerPoolFactory>();

            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
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
                UserName = "user1",
                PrefetchTaskCount = 1,
            };

            var hostBuilder = new HostBuilder();

            // Act
            using var host = hostBuilder.AddRabbitMqWorkerPool(rabbitMqSetting, new WorkerPoolSetting()
            {
                Arguments = "dummy_Arguments",
                CommandLine = "dummy_CommandLine",
                WorkerUnitCount = 5,
            }).Build();
            var serviceProvider = host.Services;

            // Assert
            var resolvedSetting = serviceProvider.GetService<MqSettingBase>();
            resolvedSetting.Should().NotBeNull();
            resolvedSetting.Should().Be(rabbitMqSetting);

            var poolFactory = serviceProvider.GetService<IWorkerPoolFactory>();
            poolFactory.Should().NotBeNull();
            poolFactory.Should().BeOfType<WorkerPoolFactory>();

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


        [Fact]
        public void AddRabbitMqWorkerPool_ShouldThrow_WhenHostBuilderIsNull()
        {
            // Arrange
            IHostBuilder hostBuilder = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                hostBuilder.AddRabbitMqWorkerPool(new RabbitMqSetting(), new WorkerPoolSetting[0]));
        }

        [Fact]
        public void AddRabbitMqWorkerPool_ShouldThrow_WhenServiceCollectionIsNull()
        {
            // Arrange
            IServiceCollection services = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                services.AddRabbitMqWorkerPool(new RabbitMqSetting(), new WorkerPoolSetting[0]));
        }

        [Fact]
        public void AddRabbitMqWorkerPool_ShouldThrow_WhenRabbitMqSettingIsNull()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                services.AddRabbitMqWorkerPool(null, new WorkerPoolSetting[0]));
        }

        [Fact]
        public void AddRabbitMqWorkerPool_ShouldThrow_WhenWorkerSettingsContainsNull()
        {
            // Arrange
            var services = new ServiceCollection();

            var workerSettings = new WorkerPoolSetting[]
            {
            new WorkerPoolSetting { WorkerUnitCount = 1 },
            null
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                services.AddRabbitMqWorkerPool(new RabbitMqSetting(), workerSettings));
        }

        [Fact]
        public void AddRabbitMqWorkerPool_workerSettingsNull_shouldThrowArgumentNullException()
        {
            // Arrange
            var hostBuilder = new HostBuilder();
            WorkerPoolSetting[] workerSettings = null;
            var rabbitMqSetting = new RabbitMqSetting { };
            Assert.Throws<ArgumentNullException>(() =>
                new HostBuilder().ConfigureServices(services =>
                {
                    services.AddLogging(); // Add logging
                    services.AddRabbitMqWorkerPool(rabbitMqSetting, workerSettings); // Register the worker pool
                }).Build()
            );
        }

            [Fact]
        public void AddRabbitMqWorkerPool_ShouldRegisterExpectedServices()
        {
            // Arrange
            var services = new ServiceCollection();
            var rabbitMqSetting = new RabbitMqSetting { HostName = "localhost" };
            var workerSettings = new WorkerPoolSetting[]
            {
                new WorkerPoolSetting { WorkerUnitCount = 5 }
            };

            services.AddLogging();

            // Act
            services.AddRabbitMqWorkerPool(rabbitMqSetting, workerSettings);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetService<IWorkerPoolFactory>().Should().NotBeNull();
            serviceProvider.GetService<MqSettingBase>().Should().NotBeNull();
            serviceProvider.GetService<WorkerPoolSetting[]>().Should().NotBeNull();
            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
        }

        [Fact]
        public void AddRabbitMqWorkerPool_HostBuilder_ShouldRegisterServices()
        {
            // Arrange
            var hostBuilder = new HostBuilder();
            var rabbitMqSetting = new RabbitMqSetting { HostName = "localhost" };
            var workerSettings = new WorkerPoolSetting[]
            {
                new WorkerPoolSetting { WorkerUnitCount = 5 }
            };

            // Act
            hostBuilder.AddRabbitMqWorkerPool(rabbitMqSetting, workerSettings);
            var host = hostBuilder.Build();
            var serviceProvider = host.Services;

            // Assert
            serviceProvider.GetService<IWorkerPoolFactory>().Should().NotBeNull();
            serviceProvider.GetService<MqSettingBase>().Should().NotBeNull();
            serviceProvider.GetService<WorkerPoolSetting[]>().Should().NotBeNull();
            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
        }

        [Fact]
        public void AddKafkaMqWorkerPool_WithValidParameters_ConfiguresServicesCorrectly()
        {
            // Arrange
            var kafkaSetting = new KafkaSetting<string> {  };
            var workerSettings = new[]
            {
                new WorkerPoolSetting()
                {
                    Arguments = "dummy_Arguments",
                    CommandLine = "dummy_CommandLine",
                    WorkerUnitCount = 5,
                    QueueName = "queue1"
                }
            };


            // Verify singleton registrations

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(); // Add logging
                    services.AddKafkaMqWorkerPool(kafkaSetting, workerSettings); // Register the worker pool
                }).Build();

            var serviceProvider = host.Services;

            var expectSetting = serviceProvider.GetService<MqSettingBase>();
            var workerSetting = serviceProvider.GetService<WorkerPoolSetting[]>();

            workerSetting.Should().NotBeNull();
            workerSetting.Length.Should().Be(1); // Assuming one setting is registered
            workerSetting[0].Arguments.Should().Be("dummy_Arguments");
            workerSetting[0].CommandLine.Should().Be("dummy_CommandLine");
            workerSetting[0].WorkerUnitCount.Should().Be(5);
            workerSetting.Should().NotBeNull();

            expectSetting.Should().NotBeNull();
            expectSetting.Should().Be(kafkaSetting);

            var workerFactory = serviceProvider.GetService<IWorkerPoolFactory>();
            workerFactory.Should().NotBeNull();
            workerFactory.Should().BeOfType<WorkerPoolFactory>();

            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
        }

        [Fact]
        public void AddKafkaMqWorkerPool_NullServices_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceCollection services = null;
            var kafkaSetting = new KafkaSetting<string>();
            var workerSettings = new[] { new WorkerPoolSetting() };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                MessageWorkerPoolExtension.AddKafkaMqWorkerPool(services, kafkaSetting, workerSettings)
            );
        }

        [Fact]
        public void AddKafkaMqWorkerPool_NullKafkaSetting_ThrowsArgumentNullException()
        {
            // Arrange
            var services = new ServiceCollection();
            KafkaSetting<string> kafkaSetting = null;
            var workerSettings = new[] { new WorkerPoolSetting() };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                MessageWorkerPoolExtension.AddKafkaMqWorkerPool(services, kafkaSetting, workerSettings)
            );
        }

        [Fact]
        public void AddKafkaMqWorkerPool_NullWorkerSettings_ThrowsArgumentNullException()
        {
            // Arrange
            var services = new ServiceCollection();
            var kafkaSetting = new KafkaSetting<string>();
            WorkerPoolSetting[] workerSettings = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                MessageWorkerPoolExtension.AddKafkaMqWorkerPool(services, kafkaSetting, workerSettings)
            );
        }

        [Fact]
        public void AddKafkaMqWorkerPool_NullWorkerSettingInArray_ThrowsInvalidOperationException()
        {
            // Arrange
            var services = new ServiceCollection();
            var kafkaSetting = new KafkaSetting<string>();
            var workerSettings = new WorkerPoolSetting[]
            {
            new WorkerPoolSetting(),
            null
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                MessageWorkerPoolExtension.AddKafkaMqWorkerPool(services, kafkaSetting, workerSettings)
            );
        }

        [Fact]
        public void AddKafkaMqWorkerPoolHostBuilder_WithValidParameters_ConfiguresHostBuilder()
        {
            // Arrange
            var hostBuilder = new HostBuilder();
            var kafkaSetting = new KafkaSetting<string> {  };
            var workerSettings = new[]
            {
                new WorkerPoolSetting {  }
            };

            // Act
            var result = MessageWorkerPoolExtension.AddKafkaMqWorkerPool(hostBuilder, kafkaSetting, workerSettings);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void AddKafkaMqWorkerPoolHostBuilder_NullHostBuilder_ThrowsArgumentNullException()
        {
            // Arrange
            IHostBuilder hostBuilder = null;
            var kafkaSetting = new KafkaSetting<string>();
            var workerSettings = new[] { new WorkerPoolSetting() };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                MessageWorkerPoolExtension.AddKafkaMqWorkerPool(hostBuilder, kafkaSetting, workerSettings)
            );
        }

        [Fact]
        public void AddKafkaqWorkerPool_ShouldWork()
        {
            // Arrange
            var kafkaSetting = new KafkaSetting<string> { };

            var hostBuilder = new HostBuilder();

            // Act
            using var host = hostBuilder.AddKafkaMqWorkerPool(kafkaSetting, new WorkerPoolSetting[] {
                new WorkerPoolSetting()
                {
                    Arguments = "dummy_Arguments",
                    CommandLine = "dummy_CommandLine",
                    WorkerUnitCount = 5,
                }
            }).Build();
            var serviceProvider = host.Services;

            // Assert
            var resolvedSetting = serviceProvider.GetService<MqSettingBase>();
            resolvedSetting.Should().NotBeNull();
            resolvedSetting.Should().Be(kafkaSetting);

            var poolFactory = serviceProvider.GetService<IWorkerPoolFactory>();
            poolFactory.Should().NotBeNull();
            poolFactory.Should().BeOfType<WorkerPoolFactory>();

            var hostedService = serviceProvider.GetService<IHostedService>();
            hostedService.Should().NotBeNull();
            hostedService.Should().BeOfType<WorkerPoolService>();
        }
    }
}
