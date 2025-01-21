using FluentAssertions;
using MessageWorkerPool.Test.Utility;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessageWorkerPool.Test
{

    public class WorkerPoolBaseTests
    {
        [Fact]
        public async Task InitPoolAsync_ShouldInitializeCorrectNumberOfWorkers()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 3 };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);

            // Act
            await workerPool.InitPoolAsync(CancellationToken.None);

            // Assert
            Assert.Equal(3, workerPool.ProcessCount);  
        }

        [Fact]
        public async Task WaitFinishedAsync_ShouldWaitForWorkersToFinishAndDispose()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 2 };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);
            await workerPool.InitPoolAsync(CancellationToken.None);

            // Act
            var cancellationTokenSource = new CancellationTokenSource();
            await workerPool.WaitFinishedAsync(cancellationTokenSource.Token);

            // Assert
            workerPool.IsClosed.Should().BeTrue();
        }

        [Fact]
        public void Dispose_ShouldDisposeWorkers()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 2 };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);
            var mockWorker1 = new Mock<IWorker>();
            var mockWorker2 = new Mock<IWorker>();
            workerPool.AddWorker(mockWorker1.Object);
            workerPool.AddWorker(mockWorker2.Object);

            // Act
            workerPool.Dispose();

            // Assert
            mockWorker1.Verify(w => w.Dispose(), Times.Once);  // Worker 1 should be disposed
            mockWorker2.Verify(w => w.Dispose(), Times.Once);  // Worker 2 should be disposed
            workerPool.IsClosed.Should().BeTrue();
        }
    }
}
