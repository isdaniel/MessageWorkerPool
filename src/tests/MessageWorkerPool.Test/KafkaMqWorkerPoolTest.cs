using FluentAssertions;
using MessageWorkerPool.Kafka;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessageWorkerPool.Test
{
    public class KafkaMqWorkerPoolTest
    {
        [Fact]
        public void KafkaMqWorkerPool_GetWorker_ReturnsKafkaMqWorker()
        {
            var kafkaSetting = new KafkaSetting<string>();
            var workerSetting = new WorkerPoolSetting();
            var loggerFactoryMock = new Mock<ILoggerFactory>();

            var workerPool = new TestKafkaMqWorkerPool<string>(kafkaSetting, workerSetting, loggerFactoryMock.Object);

            var worker = workerPool.InvokeGetWorker();

            worker.Should().NotBeNull();
            worker.Should().BeOfType<KafkaMqWorker<string>>();
        }

        [Fact]
        public void KafkaMqWorkerPool_Dispose_DoesNotThrowException()
        {
            var kafkaSetting = new KafkaSetting<string>();
            var workerSetting = new WorkerPoolSetting();
            var loggerFactoryMock = new Mock<ILoggerFactory>();

            var workerPool = new KafkaMqWorkerPool<string>(kafkaSetting, workerSetting, loggerFactoryMock.Object);

            Action act = () => workerPool.Dispose();

            act.Should().NotThrow();
        }

        private class TestKafkaMqWorkerPool<TKey> : KafkaMqWorkerPool<TKey>
        {
            public TestKafkaMqWorkerPool(KafkaSetting<TKey> kafkaSetting, WorkerPoolSetting workerSetting, ILoggerFactory loggerFactory)
                : base(kafkaSetting, workerSetting, loggerFactory) { }

            public IWorker InvokeGetWorker() => GetWorker();
        }
    }

}
