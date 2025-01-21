using MessageWorkerPool.RabbitMq;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;

namespace MessageWorkerPool.Test.Utility
{
    public class TestWorkerPool : WorkerPoolBase
    {
        public TestWorkerPool(WorkerPoolSetting workerSetting, ILoggerFactory loggerFactory)
            : base(workerSetting, loggerFactory) { }

        protected override IWorker GetWorker()
        {
            var mockWorker = new Mock<IWorker>();
            mockWorker.Setup(w => w.InitWorkerAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            return mockWorker.Object;
        }

        public void AddWorker(IWorker worker) {
            Workers.Add(worker);
        }
    }

    public class TestableRabbitMqWorkerPool : RabbitMqWorkerPool
    {
        public TestableRabbitMqWorkerPool(
            RabbitMqSetting rabbitMqSetting,
            WorkerPoolSetting workerSetting,
            IConnection connection,
            ILoggerFactory loggerFactory)
            : base(rabbitMqSetting, workerSetting, connection, loggerFactory)
        {
        }

        public new IWorker GetWorker()
        {
            return base.GetWorker();
        }
    }
}
