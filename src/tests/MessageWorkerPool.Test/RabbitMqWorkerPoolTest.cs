using MessageWorkerPool.RabbitMq;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace MessageWorkerPool.Test
{
    public class RabbitMqWorkerPoolTest {
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
    }
}
