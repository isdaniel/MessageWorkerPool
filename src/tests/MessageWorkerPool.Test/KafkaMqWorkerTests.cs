using Microsoft.Extensions.Logging;
using Moq;
using MessageWorkerPool.KafkaMq;
using Confluent.Kafka;
using MessageWorkerPool.Utilities;
using System.Text;
using MessageWorkerPool.Test.Utility;
using System.Text.Json;
using FluentAssertions;
using MessageWorkerPool.RabbitMq;
using System.Threading.Channels;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;

namespace MessageWorkerPool.Test
{
    public class KafkaMqWorkerTests
    {
        private readonly Mock<IConsumer<Null, string>> _mockConsumer;
        private readonly Mock<IProducer<Null, string>> _mockProducer;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<KafkaSetting<Null>> _mockKafkaSetting;
        public KafkaMqWorkerTests()
        {
            _mockConsumer = new Mock<IConsumer<Null, string>>();
            _mockProducer = new Mock<IProducer<Null, string>>();
            _mockKafkaSetting = new Mock<KafkaSetting<Null>>();
            _mockLogger = new Mock<ILogger>();

            _mockKafkaSetting.Setup(k => k.GetConsumer()).Returns(_mockConsumer.Object);
            _mockKafkaSetting.Setup(k => k.GetProducer()).Returns(_mockProducer.Object);

            var workerSetting = new WorkerPoolSetting { QueueName = "test-queue" };
        }

        private KafkaMqWorkerTester CreateWorker(WorkerPoolSetting workerSetting)
        {
            return new KafkaMqWorkerTester(workerSetting, _mockKafkaSetting.Object, _mockLogger.Object);
        }


        [Fact]
        public void VeirfyCreateProcess_ShouldBeSuccessfully()
        {
            var worker = CreateWorker(new WorkerPoolSetting());
            worker.VerifyCreateProcess(new System.Diagnostics.ProcessStartInfo());
        }
        //[Fact]
        //public async Task StartMessageConsumptionLoop_CancellationToken_ExitLoop()
        //{
        //    var worker = CreateWorker(new WorkerPoolSetting());
        //    var cts = new CancellationTokenSource(10);
        //    string message = "Test message";
        //    _mockConsumer.Setup(x => x.Consume(It.IsAny<CancellationToken>())).Returns(new ConsumeResult<Null, string>()
        //    {
        //        Message = new Message<Null, string>()
        //        {
        //            Value = message
        //        }
        //    });

        //    worker.Status.Should().Be(WorkerStatus.WaitForInit);
        //    await worker.InitWorkerAsync(cts.Token);
        //    worker.Status.Should().Be(WorkerStatus.Running);
        //    worker.GracefulReleaseCalled.Should().BeFalse();

        //    _mockLogger.Verify(l => l.Log(
        //         LogLevel.Information,
        //         It.IsAny<EventId>(),
        //         It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"StartMessageConsumptionLoop occurred OperationCanceledException! it's expected during service shutdown!")),
        //         null,
        //         It.IsAny<Func<It.IsAnyType, Exception, string>>()),
        //         Times.Once);
        //}

        [Theory]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE\",\"Status\":200}", true, false, false, int.MaxValue)]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE_WITH_REPLY\",\"Status\":201}", true, false, false, int.MaxValue)]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE\",\"Status\":200}", true, false, false, int.MaxValue, "my-reQueueName")]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"fake MESSAGE_DONE_WITH_REPLY\",\"Status\":201}", true, false, true, int.MaxValue, "my-reQueueName")]
        [InlineData("This is Test Message", "test-correlation-id", "{\"Message\":\"{\\u0022Headers\\u0022:{\\u0022TEST header\\u0022:\\u0022TEST content\\u0022},\\u0022Message\\u0022:\\u0022test msg\\u0022,\\u0022CorrelationId\\u0022:null}\",\"Status\":201}", true, false, true, int.MaxValue, "my-reQueueName123")]
        public async Task StartMessageConsumptionLoop_Shutdown_ToReplyQueue(string message, string correlationId, string outputResponse, bool expectAck, bool expectNack, bool expectRequeue, int tokenTimeout, string replyQueueName = null)
        {
            var worker = CreateWorker(new WorkerPoolSetting());
            var cts = new CancellationTokenSource(tokenTimeout);
            var expectOutputTask = JsonSerializer.Deserialize<MessageOutputTask>(outputResponse);

            _mockConsumer.Setup(x => x.Consume(It.IsAny<CancellationToken>())).Returns(new ConsumeResult<Null, string>() {
                Message = new Message<Null, string>()
                {
                    Value = message,
                    Headers = new Headers()
                    {
                        new Header("CorrelationId",Encoding.UTF8.GetBytes(correlationId)),
                        new Header("ReplyTo",Encoding.UTF8.GetBytes(replyQueueName ?? string.Empty))
                    }
                }
            });

            worker.Status.Should().Be(WorkerStatus.WaitForInit);
            await worker.InitWorkerAsync(cts.Token);
            worker.Status.Should().Be(WorkerStatus.Running);
            worker.pipeStream.Setup(x => x.WriteAsync(It.IsAny<MessageInputTask>()));
            worker.pipeStream.Setup(x => x.ReadAsync<MessageOutputTask>()).ReturnsAsync(expectOutputTask);
            //var expectOutputBytes = Encoding.UTF8.GetBytes(expectOutputTask?.Message);
            worker.pipeStream.Verify(x => x.WriteAsync(It.Is<MessageInputTask>(x => x.Message == message)), Times.AtLeastOnce);
            worker.pipeStream.Verify(x => x.ReadAsync<MessageOutputTask>(), Times.AtLeastOnce);
            await worker.GracefulShutDownAsync(CancellationToken.None);
            worker.mockProcess.Verify(x => x.Close(), Times.Once);
            worker.mockProcess.Verify(x => x.Dispose(), Times.Once);
            worker.mockProcess.Verify(x => x.WaitForExit(It.IsAny<int>()), Times.Once);
            worker.Status.Should().Be(WorkerStatus.Stopped);
            worker.GracefulReleaseCalled.Should().BeTrue();
        }
    }
}

