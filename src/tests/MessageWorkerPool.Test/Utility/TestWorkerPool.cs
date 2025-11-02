using MessageWorkerPool.RabbitMQ;
using MessageWorkerPool.Telemetry.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;

namespace MessageWorkerPool.Test.Utility
{
    public class TestWorkerPool : WorkerPoolBase
    {
        public TestWorkerPool(WorkerPoolSetting workerSetting, ILoggerFactory loggerFactory, ITelemetryManager telemetryManager = null)
            : base(workerSetting, loggerFactory, telemetryManager) { }

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

    public class TestWorkerPoolWithFailingWorker : WorkerPoolBase
    {
        public TestWorkerPoolWithFailingWorker(WorkerPoolSetting workerSetting, ILoggerFactory loggerFactory, ITelemetryManager telemetryManager = null)
            : base(workerSetting, loggerFactory, telemetryManager) { }

        protected override IWorker GetWorker()
        {
            var mockWorker = new Mock<IWorker>();
            mockWorker.Setup(w => w.InitWorkerAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Worker initialization failed"));
            return mockWorker.Object;
        }
    }

    public class TestWorkerPoolWithWorkerBase : WorkerPoolBase
    {
        public bool WorkerHasMetrics { get; private set; }

        public TestWorkerPoolWithWorkerBase(WorkerPoolSetting workerSetting, ILoggerFactory loggerFactory, ITelemetryManager telemetryManager = null)
            : base(workerSetting, loggerFactory, telemetryManager) { }

        protected override IWorker GetWorker()
        {
            var mockWorker = new Mock<IWorker>();
            mockWorker.Setup(w => w.InitWorkerAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Create a worker that will have metrics set on it
            var workerWithMetrics = mockWorker.Object;

            // We'll verify metrics are set in the test by checking Workers collection after init
            return workerWithMetrics;
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
