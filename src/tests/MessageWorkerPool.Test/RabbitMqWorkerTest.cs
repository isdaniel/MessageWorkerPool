using MessageWorkerPool.RabbitMq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MessageWorkerPool.Test.Utility;
using System.Text.Json;
using RabbitMQ.Client;
using MessageWorkerPool.Utilities;
using RabbitMQ.Client.Events;
using System.Text;

namespace MessageWorkerPool.Test
{
    public class RabbitMqWorkerTest
    {
        private readonly Mock<ILoggerFactory> _loggerFactoryMock;
        private readonly Mock<ILogger<RabbitMqWorker>> _loggerMock;
        private readonly Mock<IModel> _channel;

        public RabbitMqWorkerTest()
        {
            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _loggerMock = new Mock<ILogger<RabbitMqWorker>>();
            _channel = new Mock<IModel>();
            _loggerFactoryMock.Setup(lf => lf.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
        }

        private RabbitMqWorkerTester CreateWorker(RabbitMqSetting setting, WorkerPoolSetting workerSetting)
        {
            return new RabbitMqWorkerTester(setting, workerSetting, _channel.Object, _loggerFactoryMock.Object);
        }

        private BasicDeliverEventArgs CreateBasicDeliverEventArgs(string message, string correlationId)
        {
            var basicProperties = new Mock<IBasicProperties>();
            basicProperties.Setup(p => p.CorrelationId).Returns(correlationId);

            return new BasicDeliverEventArgs
            {
                DeliveryTag = 123,
                Body = Encoding.UTF8.GetBytes(message),
                BasicProperties = basicProperties.Object
            };
        }

        private async Task InitializeWorker(RabbitMqWorkerTester worker, CancellationTokenSource cts = null)
        {
            cts ??= new CancellationTokenSource();
            await worker.InitWorkerAsync(cts.Token);
        }

        private void VerifyLogging(string message)
        {
            _loggerMock.Verify(l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"received message:{message}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void CreateWorker_ShouldThrowNullReferenceException_WhenRabbitMqSettingIsNull()
        {
            Action act = () => CreateWorker(null, new WorkerPoolSetting());
            act.Should().Throw<ArgumentNullException>("*RabbitMqSetting*");
        }

        [Fact]
        public void CreateWorker_ShouldThrowNullReferenceException_WhenWorkerPoolSettingIsNull()
        {
            var rabbitMQSetting = new RabbitMqSetting();
            Action act = () => CreateWorker(rabbitMQSetting, null);
            act.Should().Throw<ArgumentNullException>("*WorkerPoolSetting*");
        }

        [Fact]
        public async Task CreateWorker_StartWorker_StartTimeIsOne_StatusIsRunning()
        {
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());

            worker.Status.Should().Be(WorkerStatus.WaitForInit);

            await InitializeWorker(worker);

            worker.Status.Should().Be(WorkerStatus.Running);
            worker.mockProcess.Verify(x => x.Start(), Times.Once);
            worker.mockProcess.Verify(x => x.BeginErrorReadLine(), Times.Once);
        }

        [Theory]
        [InlineData("This is Test Message", "test-correlation-id", "Invalid Json Output String", false, true,200)]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE\",\"Status\":200}", true, false,int.MaxValue)]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE_WITH_REPLY\",\"Status\":201}", true, false,int.MaxValue)]
        public async Task AsyncEventHandler_SendingMessage(string message, string correlationId, string outputResponse, bool expectAck, bool expectNack,int tokenTimeout)
        {
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
            var eventArgs = CreateBasicDeliverEventArgs(message, correlationId);
            var cts = new CancellationTokenSource(tokenTimeout);
            worker.Status.Should().Be(WorkerStatus.WaitForInit);
            await InitializeWorker(worker, cts);
            worker.Status.Should().Be(WorkerStatus.Running);
            worker.mockStandardInput.Setup(x => x.WriteLineAsync(It.IsAny<string>()));
            worker.mockStandardOutput.Setup(x => x.ReadLineAsync()).ReturnsAsync(outputResponse);

            await worker.AsyncEventHandler(worker, eventArgs);

            _channel.Verify(c => c.BasicAck(123, false), Times.Exactly(expectAck ? 1 : 0));
            _channel.Verify(c => c.BasicNack(123, false, true), Times.Exactly(expectNack ? 1 : 0));

            VerifyLogging(message);

            var expectedJson = JsonSerializer.Serialize(new MessageInputTask { Message = message, CorrelationId = correlationId });
            worker.mockStandardInput.Verify(x => x.WriteLineAsync(It.Is<string>(x => x == expectedJson)), Times.Once);
            worker.mockStandardOutput.Verify(x => x.ReadLineAsync(), Times.AtLeastOnce);
        }

        [Theory]
        [InlineData("This is Test Message", "test-correlation-id", "Invalid Json Output String", false, true, 1000)]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE\",\"Status\":200}", true, false, int.MaxValue)]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE_WITH_REPLY\",\"Status\":201}", true, false, int.MaxValue)]
        public async Task AsyncEventHandler_Shutdown_SendingMessage(string message, string correlationId, string outputResponse, bool expectAck, bool expectNack, int tokenTimeout)
        {
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
            var eventArgs = CreateBasicDeliverEventArgs(message, correlationId);
            var cts = new CancellationTokenSource(tokenTimeout);
            worker.Status.Should().Be(WorkerStatus.WaitForInit);
            await InitializeWorker(worker, cts);
            worker.Status.Should().Be(WorkerStatus.Running);
            worker.mockStandardInput.Setup(x => x.WriteLineAsync(It.IsAny<string>()));
            worker.mockStandardOutput.Setup(x => x.ReadLineAsync()).ReturnsAsync(outputResponse);

            await worker.AsyncEventHandler(worker, eventArgs);

            _channel.Verify(c => c.BasicAck(123, false), Times.Exactly(expectAck ? 1 : 0));
            _channel.Verify(c => c.BasicNack(123, false, true), Times.Exactly(expectNack ? 1 : 0));

            VerifyLogging(message);

            var expectedJson = JsonSerializer.Serialize(new MessageInputTask { Message = message, CorrelationId = correlationId });
            worker.mockStandardInput.Verify(x => x.WriteLineAsync(It.Is<string>(x => x == expectedJson)), Times.Once);
            worker.mockStandardOutput.Verify(x => x.ReadLineAsync(), Times.AtLeastOnce);

            await worker.GracefulShutDownAsync(CancellationToken.None);
            worker.mockProcess.Verify(x => x.Close(), Times.Once);
            worker.mockProcess.Verify(x => x.Dispose(), Times.Once);
            worker.mockProcess.Verify(x => x.WaitForExit(), Times.Once);
            worker.Status.Should().Be(WorkerStatus.Stopped);
            worker.Channel.Should().BeNull();
            worker.AsyncEventHandler.Should().BeNull();
            _loggerMock.Verify(l => l.Log(
                              LogLevel.Information,
                              It.IsAny<EventId>(),
                              It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"RabbitMQ Conn Closed!!!!")),
                              null,
                              It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                              Times.Once);
        }

        [Fact]
        public async Task AsyncEventHandler_ShouldNackWhenEmptyOutput()
        {
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
            var eventArgs = CreateBasicDeliverEventArgs("This is Test Message", "test-correlation-id");

            var cts = new CancellationTokenSource(300);
            await InitializeWorker(worker, cts);

            worker.mockStandardInput.Setup(x => x.WriteLineAsync(It.IsAny<string>()));
            worker.mockStandardOutput.Setup(x => x.ReadLineAsync()).ReturnsAsync(string.Empty);

            await worker.AsyncEventHandler(worker, eventArgs);

            _channel.Verify(c => c.BasicNack(123, false, true), Times.Once);
            _channel.Verify(c => c.BasicAck(123, false), Times.Never);

            VerifyLogging("This is Test Message");
        }
    }
}
