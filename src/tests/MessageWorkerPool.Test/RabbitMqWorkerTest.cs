using System.Text;
using System.Text.Json;
using FluentAssertions;
using MessageWorkerPool.RabbitMq;
using MessageWorkerPool.Test.Utility;
using MessageWorkerPool.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageWorkerPool.Test
{

    public class RabbitMqWorkerTest
    {
        private readonly Mock<ILoggerFactory> _loggerFactoryMock;
        private readonly Mock<ILogger<RabbitMqWorker>> _loggerMock;
        private readonly Mock<IModel> _channel;
        private readonly Mock<IBasicProperties> _basicProp;

        public RabbitMqWorkerTest()
        {
            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _loggerMock = new Mock<ILogger<RabbitMqWorker>>();
            _channel = new Mock<IModel>();
            _basicProp = new Mock<IBasicProperties>();
            _loggerFactoryMock.Setup(lf => lf.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
            _channel.Setup(x => x.CreateBasicProperties()).Returns(_basicProp.Object);
        }

        private RabbitMqWorkerTester CreateWorker(RabbitMqSetting setting, WorkerPoolSetting workerSetting)
        {
            return new RabbitMqWorkerTester(setting, workerSetting, _channel.Object, _loggerFactoryMock.Object);
        }

        private BasicDeliverEventArgs CreateBasicDeliverEventArgs(string message, string correlationId, ulong deliveryTag = 123, string replyQueueName = null, IDictionary<string, object> header = null)
        {
            _basicProp.Setup(p => p.CorrelationId).Returns(correlationId);
            _basicProp.Setup(p => p.ReplyTo).Returns(replyQueueName);
            _basicProp.Setup(p => p.Headers).Returns(header);
            _basicProp.Setup(p => p.ContentEncoding).Returns("utf-8");

            return new BasicDeliverEventArgs
            {
                DeliveryTag = deliveryTag,
                Body = Encoding.UTF8.GetBytes(message),
                BasicProperties = _basicProp.Object
            };
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
        public void newRabbitMqWorker_ShouldThrowNullReferenceException_WhenWorkerSettingIsNull()
        {
            Action act = () => new RabbitMqWorker(null, null, null, null);
            act.Should().Throw<ArgumentNullException>("*workerSetting*");
        }

        [Fact]
        public void newRabbitMqWorker_ShouldThrowNullReferenceException_WhenRabbitMqsettingIsNull()
        {
            Action act = () => new RabbitMqWorker(null, new WorkerPoolSetting(), null, null);
            act.Should().Throw<ArgumentNullException>("*setting*");
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

            await worker.InitWorkerAsync(CancellationToken.None);

            worker.Status.Should().Be(WorkerStatus.Running);
            worker.mockProcess.Verify(x => x.Start(), Times.Once);
            worker.mockProcess.Verify(x => x.BeginErrorReadLine(), Times.Once);
        }

        [Theory]
        [InlineData("This is Test Message", "test-correlation-id", true, false, int.MaxValue)]
        public async Task AsyncEventHandler_SendingMessage(string message, string correlationId, bool expectAck, bool expectNack, int tokenTimeout)
        {
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
            var eventArgs = CreateBasicDeliverEventArgs(message, correlationId);
            var cts = new CancellationTokenSource(tokenTimeout);
            var expectOutput = new MessageOutputTask() {
                Status = MessageStatus.MESSAGE_DONE,
                Message = message,
            };
            worker.Status.Should().Be(WorkerStatus.WaitForInit);
            await worker.InitWorkerAsync(cts.Token);
            worker.Status.Should().Be(WorkerStatus.Running);

            worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
            worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>()).ReturnsAsync(expectOutput);

            await worker.AsyncEventHandler(worker, eventArgs);

            _channel.Verify(c => c.BasicAck(123, false), Times.Exactly(expectAck ? 1 : 0));
            _channel.Verify(c => c.BasicNack(123, false, true), Times.Exactly(expectNack ? 1 : 0));

            VerifyLogging(message);

            var expectTask = new MessageInputTask { Message = message, CorrelationId = correlationId, Headers = null };
            worker.pipeStream.Verify(x => x.WriteAsync(It.Is<MessageInputTask>(
                x => x.Message == expectTask.Message
                && x.CorrelationId == expectTask.CorrelationId
                && x.OriginalQueueName == expectTask.OriginalQueueName)), Times.Once);
            worker.pipeStream.Verify(x => x.ReadAsync<MessageOutputTask>(), Times.AtLeastOnce);
        }

        [Theory]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE\",\"Status\":200}", true, false, false, int.MaxValue)]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE_WITH_REPLY\",\"Status\":201}", true, false, false, int.MaxValue)]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE\",\"Status\":200}", true, false, false, int.MaxValue, "my-reQueueName")]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE_WITH_REPLY\",\"Status\":201}", true, false, true, int.MaxValue, "my-reQueueName")]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"{\\u0022Headers\\u0022:{\\u0022TEST header\\u0022:\\u0022TEST content\\u0022},\\u0022Message\\u0022:\\u0022test msg\\u0022,\\u0022CorrelationId\\u0022:null}\",\"Status\":201}", true, false, true, int.MaxValue, "my-reQueueName123")]
        public async Task AsyncEventHandler_Shutdown_ToReplyQueue(string message, string correlationId, string outputResponse, bool expectAck, bool expectNack, bool expectRequeue, int tokenTimeout, string replyQueueName = null)
        {
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
            ulong deliveryTag = 123456;
            var eventArgs = CreateBasicDeliverEventArgs(message, correlationId, deliveryTag, replyQueueName);
            var cts = new CancellationTokenSource(tokenTimeout);
            var expectOutputTask = JsonSerializer.Deserialize<MessageOutputTask>(outputResponse);


            worker.Status.Should().Be(WorkerStatus.WaitForInit);
            await worker.InitWorkerAsync(cts.Token);
            worker.Status.Should().Be(WorkerStatus.Running);
            worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
            worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>()).ReturnsAsync(expectOutputTask);
            var expectOutputBytes = Encoding.UTF8.GetBytes(expectOutputTask?.Message);

            await worker.AsyncEventHandler(worker, eventArgs);

            _channel.Verify(c => c.BasicAck(deliveryTag, false), Times.Exactly(expectAck ? 1 : 0));
            worker.RejectMessageDeliveryTags.Count.Should().Be(expectNack ? 1 : 0);
            _channel.Verify(c => c.BasicPublish(
                string.Empty,
                replyQueueName,
                false,
                It.IsAny<IBasicProperties>(),
                It.Is<ReadOnlyMemory<byte>>(mm => mm.ToArray().SequenceEqual(expectOutputBytes))), Times.Exactly(expectRequeue ? 1 : 0));
            worker.pipeStream.Verify(x => x.ReadAsync<MessageOutputTask>(), Times.Once);
            worker.pipeStream.Verify(x => x.WriteAsync(It.IsAny<MessageInputTask>()), Times.Once);
            VerifyLogging(message);
           
            await worker.GracefulShutDownAsync(CancellationToken.None);
            worker.mockProcess.Verify(x => x.Close(), Times.Once);
            worker.mockProcess.Verify(x => x.Dispose(), Times.Once);
            worker.mockProcess.Verify(x => x.WaitForExit(It.IsAny<int>()), Times.Once);
            worker.Status.Should().Be(WorkerStatus.Stopped);
            worker.channel.Should().BeNull();
            worker.AsyncEventHandler.Should().BeNull();
            _loggerMock.Verify(l => l.Log(
                              LogLevel.Information,
                              It.IsAny<EventId>(),
                              It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"RabbitMQ Conn Closed!!!!")),
                              null,
                              It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                              Times.Once);
        }

        [Theory]
        [InlineData("This is Test Message",
            "test-correlation-id",
            "{\r\n  \"Message\": \"This is Mock Json Data\",\r\n  \"Status\": 201,\r\n  \"Headers\": {\r\n    \"CreateTimestamp\": \"2025-01-01T14:35:00Z\",\r\n    \"PreviousProcessingTimestamp\": \"2025-01-01T14:40:00Z\",\r\n\t\"Source\": \"OrderProcessingService\",\r\n    \"PreviousExecutedRows\": 123,\r\n    \"RequeueTimes\": 3\r\n  }\r\n}",
            true,  //expectAck
            false, //expectNack
            true,  //expectRequeue
            int.MaxValue,
            "replyQueue")]
        [InlineData("This is Test Message",
            "test-correlation-id",
            "{\r\n  \"Message\": \"This is Mock Json Data\",\r\n  \"Status\": 201,\r\n  \"Headers\": {\r\n    \"CreateTimestamp\": \"2025-01-01T14:35:00Z\",\r\n    \"PreviousProcessingTimestamp\": \"2025-01-01T14:40:00Z\",\r\n\t\"Source\": \"OrderProcessingService\",\r\n    \"PreviousExecutedRows\": 123,\r\n    \"RequeueTimes\": 3\r\n  }\r\n}",
            true,  //expectAck
            false, //expectNack
            false, //expectRequeue
            int.MaxValue,
            null)]
        [InlineData("This is Test Message",
            "test-correlation-id",
            "{\r\n  \"Message\": \"This is Mock Json Data\",\r\n  \"Status\": 200,\r\n  \"Headers\": {\r\n    \"CreateTimestamp\": \"2025-01-01T14:35:00Z\",\r\n    \"PreviousProcessingTimestamp\": \"2025-01-01T14:40:00Z\",\r\n\t\"Source\": \"OrderProcessingService\",\r\n    \"PreviousExecutedRows\": 123,\r\n    \"RequeueTimes\": 3\r\n  }\r\n}",
            true,  //expectAck
            false, //expectNack
            false, //expectRequeue
            int.MaxValue,
            "replyQueue")]
        [InlineData("This is Test Message",
            "test-correlation-id",
            "{\r\n  \"Message\": \"This is Mock Json Data\",\r\n  \"Status\": -1,\r\n  \"Headers\": {\r\n    \"CreateTimestamp\": \"2025-01-01T14:35:00Z\",\r\n    \"PreviousProcessingTimestamp\": \"2025-01-01T14:40:00Z\",\r\n\t\"Source\": \"OrderProcessingService\",\r\n    \"PreviousExecutedRows\": 123,\r\n    \"RequeueTimes\": 3\r\n  }\r\n}",
            false, //expectAck
            true,  //expectNack
            false, //expectRequeue
            1000,
            "replyQueue")]
        public async Task AsyncEventHandler_WithReplyHeader(string message, string correlationId, string outputResponse, bool expectAck, bool expectNack, bool expectRequeue, int tokenTimeout, string replyQueueName)
        {
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
            var expectOutputTask = JsonSerializer.Deserialize<MessageOutputTask>(outputResponse);
            var eventArgs = CreateBasicDeliverEventArgs(message, correlationId, replyQueueName: replyQueueName, header: expectOutputTask.Headers);
            var cts = new CancellationTokenSource(tokenTimeout);
            worker.Status.Should().Be(WorkerStatus.WaitForInit);
            await worker.InitWorkerAsync(cts.Token);
            worker.Status.Should().Be(WorkerStatus.Running);

            worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
            worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>()).ReturnsAsync(expectOutputTask);

            await worker.AsyncEventHandler(worker, eventArgs);


            _channel.Verify(c => c.BasicAck(123, false), Times.Exactly(expectAck ? 1 : 0));
            _channel.Verify(c => c.BasicNack(123, false, true), Times.Exactly(expectNack ? 1 : 0));
            worker.pipeStream.Verify(x => x.ReadAsync<MessageOutputTask>(), expectNack ? Times.AtLeastOnce: Times.Once);
            worker.pipeStream.Verify(x => x.WriteAsync(It.IsAny<MessageInputTask>()), Times.Once);
            VerifyLogging(message);


            _channel.Verify(x => x.BasicPublish(string.Empty,
                It.Is<string>(x => x == replyQueueName),
                It.IsAny<bool>(),
                It.Is<IBasicProperties>(x => x.ContentEncoding == "utf-8" && x.Headers.SequenceEqual(expectOutputTask.Headers)),
                It.Is<ReadOnlyMemory<byte>>(mm => mm.ToArray().SequenceEqual(Encoding.UTF8.GetBytes(expectOutputTask.Message)))), Times.Exactly(expectRequeue ? 1 : 0));
        }

        [Fact]
        public async Task AsyncEventHandler_ShouldNackWhenEmptyOutput()
        {
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
            var eventArgs = CreateBasicDeliverEventArgs("This is Test Message", "test-correlation-id");

            var cts = new CancellationTokenSource(300);
            await worker.InitWorkerAsync(cts.Token);

            worker.mockStandardInput.Setup(x => x.WriteLineAsync(It.IsAny<string>()));
            worker.mockStandardOutput.Setup(x => x.ReadLineAsync()).ReturnsAsync(string.Empty);

            await worker.AsyncEventHandler(worker, eventArgs);

            _channel.Verify(c => c.BasicNack(123, false, true), Times.Once);
            _channel.Verify(c => c.BasicAck(123, false), Times.Never);

            VerifyLogging("This is Test Message");
        }
    }
}
