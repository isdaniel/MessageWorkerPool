using MessageWorkerPool.RabbitMq;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Microsoft.Extensions.Hosting;

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
