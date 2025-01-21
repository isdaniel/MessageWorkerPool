using MessageWorkerPool.RabbitMq;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using MessageWorkerPool.Test.Utility;

namespace MessageWorkerPool.Test
{
    public class RabbitMqWorkerPoolTest
    {
        private readonly Mock<ILoggerFactory> _loggerFactoryMock;
        private readonly Mock<ILogger<RabbitMqWorkerPool>> _loggerMock;
        public RabbitMqWorkerPoolTest()
        {
            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _loggerMock = new Mock<ILogger<RabbitMqWorkerPool>>();
            _loggerFactoryMock.Setup(lf => lf.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenRabbitMqSettingIsNull()
        {
            // Act
            Action act = () => new RabbitMqWorkerPool(null, null, _loggerFactoryMock.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithMessage("*rabbitMqSetting*");
        }

        [Fact]
        public void Constructor_ShouldInitializeFields_WhenArgumentsAreValid()
        {
            // Arrange
            var rabbitMqSetting = new RabbitMqSetting();

            var workerSetting = new WorkerPoolSetting();

            var connectionMock = new Mock<IConnection>();
            var loggerFactoryMock = new Mock<ILoggerFactory>();

            // Act
            var workerPool = new RabbitMqWorkerPool(
                rabbitMqSetting,
                workerSetting,
                connectionMock.Object,
                loggerFactoryMock.Object
            );

            // Assert
            workerPool.Should().NotBeNull();
            workerPool.Connection.Should().Be(connectionMock.Object);
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenRabbitMqSettingIsNull_FromAnotherConstructor()
        {
            // Arrange
            RabbitMqSetting rabbitMqSetting = null;
            var workerSetting = new WorkerPoolSetting();
            var connectionMock = new Mock<IConnection>();
            var loggerFactoryMock = new Mock<ILoggerFactory>();

            // Act
            Action act = () => new RabbitMqWorkerPool(
                rabbitMqSetting,
                workerSetting,
                connectionMock.Object,
                loggerFactoryMock.Object
            );

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'rabbitMqSetting')");
        }

        [Fact]
        public void Constructor_ShouldNotThrow_WhenAllArgumentsAreValid()
        {
            // Arrange
            var rabbitMqSetting = new RabbitMqSetting();

            var workerSetting = new WorkerPoolSetting();

            var connectionMock = new Mock<IConnection>();
            var loggerFactoryMock = new Mock<ILoggerFactory>();

            // Act
            Action act = () => new RabbitMqWorkerPool(
                rabbitMqSetting,
                workerSetting,
                connectionMock.Object,
                loggerFactoryMock.Object
            );

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenConnectionIsNull()
        {
            // Arrange
            var rabbitMqSetting = new RabbitMqSetting();

            var workerSetting = new WorkerPoolSetting();

            IConnection connection = null;
            var loggerFactoryMock = new Mock<ILoggerFactory>();

            // Act
            Action act = () => new RabbitMqWorkerPool(
                rabbitMqSetting,
                workerSetting,
                connection,
                loggerFactoryMock.Object
            );

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'connection')");
        }




        [Fact]
        public void GetWorker_ShouldReturnRabbitMqWorker()
        {
            // Arrange
            var rabbitMqSetting = new RabbitMqSetting();

            var workerSetting = new WorkerPoolSetting();

            var connectionMock = new Mock<IConnection>();
            var channelMock = new Mock<IModel>();
            connectionMock.Setup(c => c.CreateModel()).Returns(channelMock.Object);

            var loggerFactoryMock = new Mock<ILoggerFactory>();

            var workerPool = new TestableRabbitMqWorkerPool(
                rabbitMqSetting,
                workerSetting,
                connectionMock.Object,
                loggerFactoryMock.Object
            );

            // Act
            var worker = workerPool.GetWorker();

            // Assert
            worker.Should().NotBeNull();
            worker.Should().BeOfType<RabbitMqWorker>();
        }

        [Fact]
        public void GetWorker_ShouldThrowException_WhenCreateModelFails()
        {
            // Arrange
            var rabbitMqSetting = new RabbitMqSetting();

            var workerSetting = new WorkerPoolSetting();

            var connectionMock = new Mock<IConnection>();
            connectionMock.Setup(c => c.CreateModel()).Throws(new InvalidOperationException("Channel creation failed"));

            var loggerFactoryMock = new Mock<ILoggerFactory>();

            var workerPool = new TestableRabbitMqWorkerPool(
                rabbitMqSetting,
                workerSetting,
                connectionMock.Object,
                loggerFactoryMock.Object
            );

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => workerPool.GetWorker());
            connectionMock.Verify(c => c.CreateModel(), Times.Once);
        }

        [Fact]
        public void GetWorker_ShouldInitializeRabbitMqWorkerCorrectly()
        {
            // Arrange
            var rabbitMqSetting = new RabbitMqSetting();

            var workerSetting = new WorkerPoolSetting();

            var connectionMock = new Mock<IConnection>();
            var channelMock = new Mock<IModel>();
            connectionMock.Setup(c => c.CreateModel()).Returns(channelMock.Object);

            var loggerFactoryMock = new Mock<ILoggerFactory>();

            var workerPool = new TestableRabbitMqWorkerPool(
                rabbitMqSetting,
                workerSetting,
                connectionMock.Object,
                loggerFactoryMock.Object
            );

            // Act
            var worker = workerPool.GetWorker();

            // Assert
            worker.Should().NotBeNull();
            worker.Should().BeOfType<RabbitMqWorker>();

            var rabbitWorker = worker.As<RabbitMqWorker>();
            rabbitWorker.Setting.Should().Be(rabbitMqSetting);
            rabbitWorker.channel.Should().Be(channelMock.Object);
        }

        [Fact]
        public void GetWorker_ShouldCallCreateModelOnConnection()
        {
            // Arrange
            var rabbitMqSetting = new RabbitMqSetting();

            var workerSetting = new WorkerPoolSetting();

            var connectionMock = new Mock<IConnection>();
            var channelMock = new Mock<IModel>();
            connectionMock.Setup(c => c.CreateModel()).Returns(channelMock.Object);

            var loggerFactoryMock = new Mock<ILoggerFactory>();

            var workerPool = new TestableRabbitMqWorkerPool(
                rabbitMqSetting,
                workerSetting,
                connectionMock.Object,
                loggerFactoryMock.Object
            );

            // Act
            workerPool.GetWorker();

            // Assert
            connectionMock.Verify(c => c.CreateModel(), Times.Once);
        }

        [Fact]
        public void GetWorker_ShouldReturnNewInstanceOnEachCall()
        {
            // Arrange
            var rabbitMqSetting = new RabbitMqSetting();

            var workerSetting = new WorkerPoolSetting();

            var connectionMock = new Mock<IConnection>();
            var channelMock = new Mock<IModel>();
            connectionMock.Setup(c => c.CreateModel()).Returns(channelMock.Object);

            var loggerFactoryMock = new Mock<ILoggerFactory>();

            var workerPool = new TestableRabbitMqWorkerPool(
                rabbitMqSetting,
                workerSetting,
                connectionMock.Object,
                loggerFactoryMock.Object
            );

            // Act
            var worker1 = workerPool.GetWorker();
            var worker2 = workerPool.GetWorker();

            // Assert
            worker1.Should().NotBeNull();
            worker2.Should().NotBeNull();
            worker1.Should().NotBeSameAs(worker2);
        }


        [Fact]
        public void Constructor_ShouldThrowUriFormatException_AndLogError_WhenRabbitMqSettingHasInvalidUri()
        {
            string Host = "localhost";

            // Act
            Action act = () => new RabbitMqWorkerPool(new RabbitMqSetting() { HostName = Host }, null, _loggerFactoryMock.Object);

            // Assert
            act.Should().Throw<BrokerUnreachableException>();

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Failed to create RabbitMQ connection for host: {Host}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        [Fact]
        public void Constructor_ShouldSetConnection_WhenConnectionHandlerIsProvided()
        {
            // Arrange
            Mock<IConnection> mockConnection = new Mock<IConnection>();

            var workerPool = new RabbitMqWorkerPool(
                new RabbitMqSetting
                {
                    ConnectionHandler = setting => mockConnection.Object
                },
                new WorkerPoolSetting(),
                _loggerFactoryMock.Object
            );

            // Assert
            workerPool.Connection.Should().Be(mockConnection.Object);
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenWorkerPoolSettingIsNull()
        {
            // Arrange
            Mock<IConnection> mockConnection = new Mock<IConnection>();

            // Act
            Action act = () => new RabbitMqWorkerPool(
                new RabbitMqSetting
                {
                    ConnectionHandler = setting => mockConnection.Object
                },
                null,
                _loggerFactoryMock.Object
            );

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithMessage("*workerSetting*");
        }

        [Fact]
        public void CreateConnection_ShouldReturnValidConnection_WhenParametersAreCorrect()
        {
            // Arrange
            var mockLogger = MockLogger<RabbitMqWorkerPool>();
            var mockLoggerFactory = MockLoggerFactory<RabbitMqWorkerPool>(mockLogger);

            var mockConnection = new Mock<IConnection>();
            int invocationCount = 0;

            var mockRabbitMqSetting = new RabbitMqSetting
            {
                ConnectionHandler = (setting) =>
                {
                    invocationCount++;
                    return mockConnection.Object;
                }
            };

            // Act
            var connection = RabbitMqWorkerPool.CreateConnection(mockRabbitMqSetting, mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(connection);
            Assert.Equal(1, invocationCount); 
        }

        [Fact]
        public void Dispose_ShouldCloseConnection_WhenConnectionIsOpen()
        {
            // Arrange
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(c => c.IsOpen).Returns(true);
            mockConnection.Setup(c => c.Close());

            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockRabbitMqSetting = new Mock<RabbitMqSetting>();
            var workerSetting = new WorkerPoolSetting();

            var workerPool = new RabbitMqWorkerPool(mockRabbitMqSetting.Object, workerSetting, mockConnection.Object, mockLoggerFactory.Object);

            // Act
            workerPool.Dispose();

            // Assert
            mockConnection.Verify(c => c.Close(), Times.Once);
        }

        [Fact]
        public void Dispose_ShouldNotCloseConnection_WhenConnectionIsAlreadyClosed()
        {
            // Arrange
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(c => c.IsOpen).Returns(false);

            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockRabbitMqSetting = new Mock<RabbitMqSetting>();
            var workerSetting = new WorkerPoolSetting();

            var workerPool = new RabbitMqWorkerPool(mockRabbitMqSetting.Object, workerSetting, mockConnection.Object, mockLoggerFactory.Object);

            // Act
            workerPool.Dispose();

            // Assert
            mockConnection.Verify(c => c.Close(), Times.Never);
        }

        [Fact]
        public void CreateConnection_ShouldLogError_WhenConnectionHandlerFails()
        {
            // Arrange
            var mockLogger = MockLogger<RabbitMqWorkerPool>();
            var mockLoggerFactory = MockLoggerFactory<RabbitMqWorkerPool>(mockLogger);
            string hostName = "test-host";
            var rabbitMqSetting = new RabbitMqSetting
            {
                HostName = hostName,
                ConnectionHandler = _ => throw new Exception("Connection failed")
            };

            // Act & Assert
            Assert.Throws<Exception>(() =>
                RabbitMqWorkerPool.CreateConnection(rabbitMqSetting, mockLoggerFactory.Object));

            mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Failed to create RabbitMQ connection for host: {hostName}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        private Mock<ILogger<T>> MockLogger<T>()
        {
            return new Mock<ILogger<T>>();
        }

        private Mock<ILoggerFactory> MockLoggerFactory<T>(Mock<ILogger<T>> mockLogger)
        {
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory
                .Setup(f => f.CreateLogger(typeof(T).FullName))
                .Returns(mockLogger.Object);
            return mockLoggerFactory;
        }
    }
}
