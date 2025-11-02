using FluentAssertions;
using MessageWorkerPool.RabbitMQ;
using MessageWorkerPool.Telemetry.Abstractions;
using MessageWorkerPool.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Moq;
using RabbitMQ.Client;

namespace MessageWorkerPool.Test
{
    public class WorkerPoolFactoryTests
    {
        [Fact]
        public void CreateWorkerPool_ShouldCreateCorrectWorkerPool_WhenRabbitMqSettingIsUsed()
        {
            // Arrange
            Mock<IConnection> mockConnection = new Mock<IConnection>();

            var rabbitMqSetting = new RabbitMqSetting
            {
                ConnectionHandler = setting => mockConnection.Object
            };

            var mockLoggerFactory = new Mock<ILoggerFactory>(); // Ensure this is properly initialized
            var poolSetting = new WorkerPoolSetting { WorkerUnitCount = 5 };

            var workerPoolFactory = new WorkerPoolFactory(rabbitMqSetting, mockLoggerFactory.Object);
            workerPoolFactory.RegisterGeneric<RabbitMqSetting, RabbitMqWorkerPool>();

            // Act
            var workerPool = workerPoolFactory.CreateWorkerPool(poolSetting);

            // Assert
            var rabbitMqWorkerPool = Assert.IsType<RabbitMqWorkerPool>(workerPool);
            rabbitMqWorkerPool.ProcessCount.Should().Be(5);
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenMqSettingIsNull()
        {
            // Arrange
            MqSettingBase nullMqSetting = null;
            var mockLoggerFactory = new Mock<ILoggerFactory>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new WorkerPoolFactory(nullMqSetting, mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateWorkerPool_ShouldThrowNotSupportedException_WhenNoFactoryRegisteredForMqSetting()
        {
            // Arrange
            var mockOtherMqSetting = new Mock<MqSettingBase>();  // Mock a new, unsupported MqSetting type
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var poolSetting = new WorkerPoolSetting { WorkerUnitCount = 2 };

            var workerPoolFactory = new WorkerPoolFactory(mockOtherMqSetting.Object, mockLoggerFactory.Object);

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => workerPoolFactory.CreateWorkerPool(poolSetting));
        }

        [Fact]
        public void CreateWorkerPool_ShouldReturnCorrectWorkerPool_ForSingleQueueType()
        {
            // Arrange
            Mock<IConnection> mockConnection = new Mock<IConnection>();

            var rabbitMqSetting = new RabbitMqSetting
            {
                ConnectionHandler = setting => mockConnection.Object
            };
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var poolSetting = new WorkerPoolSetting { WorkerUnitCount = 4 };

            var workerPoolFactory = new WorkerPoolFactory(rabbitMqSetting, mockLoggerFactory.Object);
            workerPoolFactory.RegisterGeneric<RabbitMqSetting, RabbitMqWorkerPool>();

            // Act
            var workerPool = workerPoolFactory.CreateWorkerPool(poolSetting);

            // Assert
            var rabbitMqWorkerPool = Assert.IsType<RabbitMqWorkerPool>(workerPool);
            Assert.Equal(4, rabbitMqWorkerPool.ProcessCount);
        }

        [Fact]
        public void CreateWorkerPool_ShouldHandleMultipleWorkerPoolSettings_Correctly()
        {
            // Arrange
            Mock<IConnection> mockConnection = new Mock<IConnection>();

            var rabbitMqSetting = new RabbitMqSetting
            {
                ConnectionHandler = setting => mockConnection.Object
            };

            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var poolSetting1 = new WorkerPoolSetting { WorkerUnitCount = 3 };
            var poolSetting2 = new WorkerPoolSetting { WorkerUnitCount = 5 };

            var workerPoolFactory = new WorkerPoolFactory(rabbitMqSetting, mockLoggerFactory.Object);
            workerPoolFactory.RegisterGeneric<RabbitMqSetting, RabbitMqWorkerPool>();

            // Act
            var workerPool1 = workerPoolFactory.CreateWorkerPool(poolSetting1);
            var workerPool2 = workerPoolFactory.CreateWorkerPool(poolSetting2);

            // Assert
            var rabbitMqWorkerPool1 = Assert.IsType<RabbitMqWorkerPool>(workerPool1);
            var rabbitMqWorkerPool2 = Assert.IsType<RabbitMqWorkerPool>(workerPool2);

            rabbitMqWorkerPool1.ProcessCount.Should().Be(3);
            rabbitMqWorkerPool2.ProcessCount.Should().Be(5);
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenLoggerFactoryIsNull()
        {
            // Arrange
            var mockRabbitMqSetting = new Mock<RabbitMqSetting>();
            ILoggerFactory nullLoggerFactory = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new WorkerPoolFactory(mockRabbitMqSetting.Object, nullLoggerFactory));
        }

        [Fact]
        public void CreateWorkerPool_ShouldUseProvidedPoolSetting_WhenCreatingWorkerPool()
        {
            // Arrange
            Mock<IConnection> mockConnection = new Mock<IConnection>();

            var rabbitMqSetting = new RabbitMqSetting
            {
                ConnectionHandler = setting => mockConnection.Object
            };
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var poolSetting = new WorkerPoolSetting { WorkerUnitCount = 5 }; // Providing custom pool settings

            var workerPoolFactory = new WorkerPoolFactory(rabbitMqSetting, mockLoggerFactory.Object);
            workerPoolFactory.RegisterGeneric<RabbitMqSetting, RabbitMqWorkerPool>();

            // Act
            var workerPool = workerPoolFactory.CreateWorkerPool(poolSetting);

            // Assert
            var rabbitMqWorkerPool = Assert.IsType<RabbitMqWorkerPool>(workerPool);
            rabbitMqWorkerPool.ProcessCount.Should().Be(5);
        }

        [Fact]
        public void CreateWorkerPool_ShouldThrowNotSupportedException_WhenNoFactoryRegistered()
        {
            // Arrange
            var mockMqSetting = new Mock<MqSettingBase>();  // This is not registered in the factory
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var poolSetting = new WorkerPoolSetting { WorkerUnitCount = 2 };

            var workerPoolFactory = new WorkerPoolFactory(mockMqSetting.Object, mockLoggerFactory.Object);

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => workerPoolFactory.CreateWorkerPool(poolSetting));
        }

        [Fact]
        public void RegisterGeneric_ShouldRegisterNewWorkerPool()
        {
            var _mockLoggerFactory = new Mock<ILoggerFactory>();
            var poolSetting = new WorkerPoolSetting();
            var customMqSetting = new CustomMqSetting<string>();
            var factory = new WorkerPoolFactory(customMqSetting, _mockLoggerFactory.Object);
            factory.RegisterGeneric<CustomMqSetting<string>, CustomWorkerPool<string>>();

            var workerPool = factory.CreateWorkerPool(poolSetting);
            workerPool.Should().NotBeNull();
            workerPool.Should().BeOfType<CustomWorkerPool<string>>();
        }
    }

    class CustomMqSetting<TKey> : MqSettingBase
    {
        public override string GetConnectionString()
        {
            throw new NotImplementedException();
        }
    }

    class CustomWorkerPool<TKey> : IWorkerPool
    {
        public CustomWorkerPool(CustomMqSetting<TKey> setting, WorkerPoolSetting poolSetting, ILoggerFactory loggerFactory, ITelemetryManager telemetryManager) { }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task InitPoolAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task WaitFinishedAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
