using Microsoft.Extensions.Logging;
using Moq;

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
}
