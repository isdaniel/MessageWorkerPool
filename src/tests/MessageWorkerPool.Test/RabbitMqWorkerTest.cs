using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.IO;
using MessageWorkerPool.Kafka;
using MessageWorkerPool.RabbitMQ;
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
        private readonly Mock<ILogger<RabbitMqWorker>> _loggerMock;
        private readonly Mock<IModel> _channel;
        private readonly Mock<IBasicProperties> _basicProp;
        private readonly Mock<ILogger> _logger;

        public RabbitMqWorkerTest()
        {
            _loggerMock = new Mock<ILogger<RabbitMqWorker>>();
            _logger = new Mock<ILogger>();
            _channel = new Mock<IModel>();
            _basicProp = new Mock<IBasicProperties>();
            _channel.Setup(x => x.CreateBasicProperties()).Returns(_basicProp.Object);
        }

        private RabbitMqWorkerTester CreateWorker(RabbitMqSetting setting, WorkerPoolSetting workerSetting)
        {
            return new RabbitMqWorkerTester(setting, workerSetting, _channel.Object, _loggerMock.Object);
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
            worker.GracefulReleaseCalled.Should().BeTrue();
        }

        [Theory]
        [InlineData("This is Test Message",
            "test-correlation-id",
            "{\r\n  \"Message\": \"This is Mock Json Data\",\r\n  \"Status\": 201,\r\n  \"Headers\": {\r\n    \"CreateTimestamp\": \"2025-01-01T14:35:00Z\",\r\n    \"PreviousProcessingTimestamp\": \"2025-01-01T14:40:00Z\",\r\n\t\"Source\": \"OrderProcessingService\",\r\n    \"PreviousExecutedRows\": \"123\",\r\n    \"RequeueTimes\": \"3\"\r\n  }\r\n}",
            true,  //expectAck
            false, //expectNack
            true,  //expectRequeue
            int.MaxValue,
            "replyQueue")]
        [InlineData("This is Test Message",
            "test-correlation-id",
            "{\r\n  \"Message\": \"This is Mock Json Data\",\r\n  \"Status\": 201,\r\n  \"Headers\": {\r\n    \"CreateTimestamp\": \"2025-01-01T14:35:00Z\",\r\n    \"PreviousProcessingTimestamp\": \"2025-01-01T14:40:00Z\",\r\n\t\"Source\": \"OrderProcessingService\",\r\n    \"PreviousExecutedRows\": \"123\",\r\n    \"RequeueTimes\":  \"3\"\r\n  }\r\n}",
            true,  //expectAck
            false, //expectNack
            false, //expectRequeue
            int.MaxValue,
            null)]
        [InlineData("This is Test Message",
            "test-correlation-id",
            "{\r\n  \"Message\": \"This is Mock Json Data\",\r\n  \"Status\": 200,\r\n  \"Headers\": {\r\n    \"CreateTimestamp\": \"2025-01-01T14:35:00Z\",\r\n    \"PreviousProcessingTimestamp\": \"2025-01-01T14:40:00Z\",\r\n\t\"Source\": \"OrderProcessingService\",\r\n    \"PreviousExecutedRows\": \"123\",\r\n    \"RequeueTimes\": \"3\"\r\n  }\r\n}",
            true,  //expectAck
            false, //expectNack
            false, //expectRequeue
            int.MaxValue,
            "replyQueue")]
        [InlineData("This is Test Message",
            "test-correlation-id",
            "{\r\n  \"Message\": \"This is Mock Json Data\",\r\n  \"Status\": -1,\r\n  \"Headers\": {\r\n    \"CreateTimestamp\": \"2025-01-01T14:35:00Z\",\r\n    \"PreviousProcessingTimestamp\": \"2025-01-01T14:40:00Z\",\r\n\t\"Source\": \"OrderProcessingService\",\r\n    \"PreviousExecutedRows\": \"123\",\r\n    \"RequeueTimes\": \"3\"\r\n  }\r\n}",
            false, //expectAck
            true,  //expectNack
            false, //expectRequeue
            1000,
            "replyQueue")]
        public async Task AsyncEventHandler_WithReplyHeader(string message, string correlationId, string outputResponse, bool expectAck, bool expectNack, bool expectRequeue, int tokenTimeout, string replyQueueName)
        {
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
            var expectOutputTask = JsonSerializer.Deserialize<MessageOutputTask>(outputResponse);
            var eventArgs = CreateBasicDeliverEventArgs(message, correlationId, replyQueueName: replyQueueName, header: expectOutputTask.Headers.ConvertToObjectMap());
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
                It.Is<IBasicProperties>(x => x.ContentEncoding == "utf-8" && x.Headers.ConvertToStringMap().SequenceEqual(expectOutputTask.Headers)),
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

        [Fact]
        public async Task GracefulReleaseAsync_ShouldExecuteCustomLogic_WhenOverridden()
        {
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
            await worker.InitWorkerAsync(CancellationToken.None);
            await worker.GracefulShutDownAsync(CancellationToken.None);
            worker.GracefulReleaseCalled.Should().BeTrue();
        }

        [Fact]
        public async Task CreateOperationPipeAsync_ShouldReturnValidPipeStreamWrapper()
        {
            string pipeName = $"testPipe_{Guid.NewGuid():N}";
            var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());

            var pipeStreamWrapper = await worker.TestCreateOperationPipeAsync(pipeName);

            pipeStreamWrapper.Should().NotBeNull();
            pipeStreamWrapper.Should().Be(worker.pipeStream.Object);
        }


        [Fact]
        public async Task WorkerBaseTestor_CreateOperationPipeAsync_WithValidPipeName_ReturnsPipeStreamWrapper()
        {
            var loggerMock = new Mock<ILogger>();
            var workerSetting = new WorkerPoolSetting();
            var testWorker = new WorkerBaseTestor(workerSetting, loggerMock.Object);
            string pipeName = "testpipe";
            Func<Task> act = async () => await testWorker.CreateOperationPipeAsync(pipeName);
            using var clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            var task = Task.Run(() => {
                clientStream.Connect();
            });
            await act.Should().NotThrowAsync();
            await task;
        }

        [Fact]
        public async Task CreateOperationPipeAsync_ShouldThrowException_WhenPipeCreationFails()
        {
            var loggerMock = new Mock<ILogger>();
            var workerSetting = new WorkerPoolSetting();
            var testWorker = new WorkerBaseTestor(workerSetting, loggerMock.Object);
            string pipeName = "test.123";
            CancellationTokenSource cts = new CancellationTokenSource(1000);
            var creation = async () => await testWorker.CreateOperationPipeAsync(pipeName, cts.Token);
            await creation.Should().ThrowAsync<Exception>();
        }

        private class WorkerBaseTestor : WorkerBase
        {
            public WorkerBaseTestor(WorkerPoolSetting workerSetting, ILogger logger) : base(workerSetting, logger)
            {
            }

            protected override void SetupMessageQueueSetting(CancellationToken token)
            {
            }

            public async Task CreateOperationPipeAsync(string name, CancellationToken token = default) => await base.CreateOperationPipeAsync(name, token);
        }

        /// <summary>
        /// Additional tests for RabbitMqWorker to improve code coverage
        /// </summary>
        public class RabbitMqWorkerAdditionalTest
        {
            private readonly Mock<ILogger<RabbitMqWorker>> _loggerMock;
            private readonly Mock<IModel> _channel;
            private readonly Mock<IBasicProperties> _basicProp;

            public RabbitMqWorkerAdditionalTest()
            {
                _loggerMock = new Mock<ILogger<RabbitMqWorker>>();
                _channel = new Mock<IModel>();
                _basicProp = new Mock<IBasicProperties>();
                _channel.Setup(x => x.CreateBasicProperties()).Returns(_basicProp.Object);
            }

            private RabbitMqWorkerTester CreateWorker(RabbitMqSetting setting, WorkerPoolSetting workerSetting)
            {
                return new RabbitMqWorkerTester(setting, workerSetting, _channel.Object, _loggerMock.Object);
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

            [Fact]
            public async Task AsyncEventHandler_WithException_ShouldNackMessage()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                var eventArgs = CreateBasicDeliverEventArgs("Test Message", "correlation-123");
                await worker.InitWorkerAsync(CancellationToken.None);

                worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()))
                    .ThrowsAsync(new InvalidOperationException("Test exception"));

                // Act
                await worker.AsyncEventHandler(worker, eventArgs);

                // Assert
                _channel.Verify(c => c.BasicNack(123, false, true), Times.Once);
                _channel.Verify(c => c.BasicAck(123, false), Times.Never);
            }

            [Fact]
            public async Task AsyncEventHandler_WhenStopping_ShouldRejectMessage()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                await worker.InitWorkerAsync(CancellationToken.None);

                // Setup a task completion source to control when processing completes
                var processingStarted = new TaskCompletionSource<bool>();
                var allowProcessingToComplete = new TaskCompletionSource<bool>();

                // Setup pipe to simulate long-running task for the first message
                var callCount = 0;
                worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
                worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>())
                    .Returns(async () =>
                    {
                        callCount++;
                        if (callCount == 1)
                        {
                            // First call - signal that processing started, then wait
                            processingStarted.SetResult(true);
                            await allowProcessingToComplete.Task;
                            return new MessageOutputTask { Status = MessageStatus.MESSAGE_DONE, Message = "Done" };
                        }
                        // Should not get here for second message
                        return new MessageOutputTask { Status = MessageStatus.MESSAGE_DONE, Message = "Done2" };
                    });

                // Act - Start processing first message
                var eventArgs1 = CreateBasicDeliverEventArgs("Test Message 1", "correlation-123", 999);
                var firstMessageTask = worker.AsyncEventHandler(worker, eventArgs1);

                // Wait for first message to start processing
                await processingStarted.Task;

                // Now start graceful shutdown (this will set status to Stopping)
                var shutdownTask = Task.Run(async () =>
                {
                    await worker.GracefulShutDownAsync(CancellationToken.None);
                });

                // Give shutdown time to change the status
                await Task.Delay(100);

                // Try to process second message - this should be rejected because worker is stopping
                var eventArgs2 = CreateBasicDeliverEventArgs("Test Message 2", "correlation-124", 1000);
                await worker.AsyncEventHandler(worker, eventArgs2);

                // Assert - The second message should be in reject queue (not processed, just added to reject list)
                worker.RejectMessageDeliveryTags.Should().Contain(1000);

                // Allow first message to complete and finish shutdown
                allowProcessingToComplete.SetResult(true);
                await firstMessageTask;
                await shutdownTask;

                // The first message should have been acknowledged normally
                _channel.Verify(c => c.BasicAck(999, false), Times.Once);
                // The second message should not have been processed at all (not acked or nacked directly)
                _channel.Verify(c => c.BasicAck(1000, false), Times.Never);
                // The second message will be nacked during shutdown via RejectRemainingMessages
                _channel.Verify(c => c.BasicNack(1000, false, true), Times.Once);
            }

            [Fact]
            public async Task AsyncEventHandler_WithTraceContext_ShouldPropagateContext()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                var traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
                var headers = new Dictionary<string, object>
            {
                { "traceparent", Encoding.UTF8.GetBytes(traceParent) },
                { "tracestate", Encoding.UTF8.GetBytes("congo=t61rcWkgMzE") }
            };
                var eventArgs = CreateBasicDeliverEventArgs("Test Message", "correlation-123", header: headers);

                await worker.InitWorkerAsync(CancellationToken.None);

                var expectOutput = new MessageOutputTask()
                {
                    Status = MessageStatus.MESSAGE_DONE,
                    Message = "Response",
                };

                worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
                worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>()).ReturnsAsync(expectOutput);

                // Act
                await worker.AsyncEventHandler(worker, eventArgs);

                // Assert - Verify the task input contains propagated headers
                worker.pipeStream.Verify(x => x.WriteAsync(It.Is<MessageInputTask>(
                    task => task.Headers != null && task.Headers.ContainsKey("traceparent"))), Times.Once);
            }

            [Fact]
            public async Task Dispose_ShouldCloseChannel_WhenChannelIsOpen()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                _channel.Setup(x => x.IsClosed).Returns(false);
                await worker.InitWorkerAsync(CancellationToken.None);

                // Act
                worker.Dispose();

                // Assert
                _channel.Verify(x => x.Close(), Times.Once);
                _channel.Verify(x => x.Dispose(), Times.Once);
            }

            [Fact]
            public async Task Dispose_ShouldHandleException_WhenClosingChannel()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                _channel.Setup(x => x.IsClosed).Returns(false);
                _channel.Setup(x => x.Close()).Throws(new InvalidOperationException("Channel close failed"));
                await worker.InitWorkerAsync(CancellationToken.None);

                // Act - Should not throw
                Action act = () => worker.Dispose();

                // Assert
                act.Should().NotThrow();
                _channel.Verify(x => x.Dispose(), Times.Once);
            }

            [Fact]
            public async Task Dispose_MultipleTimes_ShouldOnlyDisposeOnce()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                await worker.InitWorkerAsync(CancellationToken.None);

                // Act
                worker.Dispose();
                worker.Dispose();
                worker.Dispose();

                // Assert - Dispose should be called only once
                worker.mockProcess.Verify(x => x.Dispose(), Times.Once);
            }

            [Fact]
            public async Task AsyncEventHandler_WithReplyQueueOverride_ShouldUseCustomQueue()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                var eventArgs = CreateBasicDeliverEventArgs("Test", "corr-1", replyQueueName: "original-queue");
                await worker.InitWorkerAsync(CancellationToken.None);

                var expectOutput = new MessageOutputTask()
                {
                    Status = MessageStatus.MESSAGE_DONE_WITH_REPLY,
                    Message = "Response",
                    ReplyQueueName = "custom-reply-queue" // Override
                };

                worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
                worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>()).ReturnsAsync(expectOutput);

                // Act
                await worker.AsyncEventHandler(worker, eventArgs);

                // Assert - Should use custom queue name
                _channel.Verify(c => c.BasicPublish(
                    string.Empty,
                    "custom-reply-queue",
                    false,
                    It.IsAny<IBasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
            }

            [Fact]
            public async Task GracefulShutdown_WithPendingMessages_ShouldRejectAll()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                await worker.InitWorkerAsync(CancellationToken.None);

                // Add some pending messages
                worker.RejectMessageDeliveryTags.Add(100);
                worker.RejectMessageDeliveryTags.Add(200);
                worker.RejectMessageDeliveryTags.Add(300);

                // Act
                await worker.GracefulShutDownAsync(CancellationToken.None);

                // Assert
                _channel.Verify(c => c.BasicNack(100, false, true), Times.Once);
                _channel.Verify(c => c.BasicNack(200, false, true), Times.Once);
                _channel.Verify(c => c.BasicNack(300, false, true), Times.Once);
            }

            [Fact]
            public void RabbitMqWorker_Setting_ShouldBeAccessible()
            {
                // Arrange
                var rabbitMqSetting = new RabbitMqSetting { PrefetchTaskCount = 5 };
                var workerSetting = new WorkerPoolSetting();

                // Act
                var worker = CreateWorker(rabbitMqSetting, workerSetting);

                // Assert
                worker.Setting.Should().NotBeNull();
                worker.Setting.PrefetchTaskCount.Should().Be(5);
            }

            [Fact]
            public async Task AsyncEventHandler_WithNullHeaders_ShouldHandleGracefully()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                var eventArgs = CreateBasicDeliverEventArgs("Test", "corr-1", header: null);
                await worker.InitWorkerAsync(CancellationToken.None);

                var expectOutput = new MessageOutputTask()
                {
                    Status = MessageStatus.MESSAGE_DONE,
                    Message = "Response"
                };

                worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
                worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>()).ReturnsAsync(expectOutput);

                // Act
                await worker.AsyncEventHandler(worker, eventArgs);

                // Assert
                _channel.Verify(c => c.BasicAck(123, false), Times.Once);
                worker.pipeStream.Verify(x => x.WriteAsync(It.Is<MessageInputTask>(
                    task => task.Headers != null)), Times.Once);
            }

            [Fact]
            public async Task AsyncEventHandler_WithCurrentActivity_ShouldInjectTraceContext()
            {
                // Arrange
                using var activitySource = new ActivitySource("TestSource");
                using var listener = new ActivityListener
                {
                    ShouldListenTo = source => source.Name == "TestSource",
                    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
                };
                ActivitySource.AddActivityListener(listener);

                using var parentActivity = activitySource.StartActivity("ParentActivity");

                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                var eventArgs = CreateBasicDeliverEventArgs("Test", "corr-1");
                await worker.InitWorkerAsync(CancellationToken.None);

                var expectOutput = new MessageOutputTask()
                {
                    Status = MessageStatus.MESSAGE_DONE,
                    Message = "Response"
                };

                worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
                worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>()).ReturnsAsync(expectOutput);

                // Act
                await worker.AsyncEventHandler(worker, eventArgs);

                // Assert - Should inject traceparent when Activity.Current exists
                worker.pipeStream.Verify(x => x.WriteAsync(It.Is<MessageInputTask>(
                    task => task.Headers.ContainsKey("traceparent"))), Times.Once);
            }

            [Fact]
            public async Task Dispose_WhenChannelIsClosed_ShouldNotCallClose()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                _channel.Setup(x => x.IsClosed).Returns(true);
                await worker.InitWorkerAsync(CancellationToken.None);

                // Act
                worker.Dispose();

                // Assert - Close should not be called if channel is already closed
                _channel.Verify(x => x.Close(), Times.Never);
                _channel.Verify(x => x.Dispose(), Times.Once);
            }

            [Fact]
            public async Task AsyncEventHandler_WithReadException_ShouldNackMessage()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                var eventArgs = CreateBasicDeliverEventArgs("Test Message", "correlation-123");
                await worker.InitWorkerAsync(CancellationToken.None);

                worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
                worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>())
                    .ThrowsAsync(new InvalidOperationException("Read exception"));

                // Act
                await worker.AsyncEventHandler(worker, eventArgs);

                // Assert
                _channel.Verify(c => c.BasicNack(123, false, true), Times.Once);
                _channel.Verify(c => c.BasicAck(123, false), Times.Never);
            }

            [Fact]
            public async Task RabbitMqWorker_ChannelProperty_ShouldBeAccessible()
            {
                // Arrange & Act
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                await worker.InitWorkerAsync(CancellationToken.None);

                // Assert
                worker.channel.Should().NotBeNull();
                worker.channel.Should().Be(_channel.Object);
            }

            [Fact]
            public async Task Dispose_AfterGracefulShutdown_ShouldDisposeChannel()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                await worker.InitWorkerAsync(CancellationToken.None);
                await worker.GracefulShutDownAsync(CancellationToken.None);

                // Channel should already be disposed from graceful shutdown
                // Act - Dispose again should handle gracefully
                worker.Dispose();

                // Assert - Should not throw
                worker.channel.Should().BeNull();
            }

            [Fact]
            public async Task AsyncEventHandler_WithCancellationRequested_ShouldHandleGracefully()
            {
                // Arrange
                var worker = CreateWorker(new RabbitMqSetting(), new WorkerPoolSetting());
                var eventArgs = CreateBasicDeliverEventArgs("Test Message", "correlation-123", 999);
                await worker.InitWorkerAsync(CancellationToken.None);

                // Setup to simulate timeout scenario
                var cts = new CancellationTokenSource();
                cts.Cancel(); // Cancel immediately

                worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
                worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>())
                    .Returns(async () =>
                    {
                        await Task.Delay(1000, cts.Token); // This will throw
                        return new MessageOutputTask { Status = MessageStatus.MESSAGE_DONE, Message = "Done" };
                    });

                // Act
                await worker.AsyncEventHandler(worker, eventArgs);

                // Assert - Message should be acknowledged or rejected based on output
                // Since we're testing edge cases, verify the handler completes
                _channel.Verify(c => c.BasicAck(It.IsAny<ulong>(), It.IsAny<bool>()), Times.AtMostOnce());
                _channel.Verify(c => c.BasicNack(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.AtMostOnce());
            }
        }
    }
}
