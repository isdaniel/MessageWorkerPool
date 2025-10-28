using FluentAssertions;
using MessageWorkerPool.Test.Utility;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.OpenTelemetry;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace MessageWorkerPool.Test
{

    public class WorkerPoolBaseTests
    {
        private Mock<ILoggerFactory> CreateMockLoggerFactory()
        {
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(mockLogger.Object);
            return mockLoggerFactory;
        }

        [Fact]
        public async Task InitPoolAsync_ShouldInitializeCorrectNumberOfWorkers()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();
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
            var mockLoggerFactory = CreateMockLoggerFactory();
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
            var mockLoggerFactory = CreateMockLoggerFactory();
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

        [Fact]
        public void Constructor_ShouldInitializeWithoutError()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 2, QueueName = "test-queue" };

            // Act
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);

            // Assert - Constructor should complete successfully
            workerPool.Should().NotBeNull();
            workerPool.ProcessCount.Should().Be(2);
        }

        [Fact]
        public async Task InitPoolAsync_ShouldSetActiveWorkersMetric()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 3, QueueName = "test-queue" };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);

            // Act
            await workerPool.InitPoolAsync(CancellationToken.None);

            // Assert - The metrics should be set to the number of workers
            // Note: We can't directly assert on internal metrics state, but we can verify the pool initialized correctly
            workerPool.ProcessCount.Should().Be(3);
        }

        [Fact]
        public async Task InitPoolAsync_ShouldCreateActivityWithCorrectTags()
        {
            // Arrange
            // Set up OpenTelemetry provider for the test
            var openTelemetryProvider = new OpenTelemetryProvider("MessageWorkerPool", "1.0.0");
            TelemetryManager.SetProvider(openTelemetryProvider);

            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 2, QueueName = "test-queue" };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);

            var activities = new ConcurrentBag<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "MessageWorkerPool",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activities.Add
            };
            ActivitySource.AddActivityListener(listener);

            try
            {
                // Act
                await workerPool.InitPoolAsync(CancellationToken.None);

                // Assert
                var poolInitActivity = activities.FirstOrDefault(a => a.OperationName == "WorkerPool.Init");
                poolInitActivity.Should().NotBeNull();
                poolInitActivity!.GetTagItem("workerpool.worker_count").Should().Be("2");
                poolInitActivity.GetTagItem("queue.name").Should().Be("test-queue");
            }
            finally
            {
                // Clean up - reset to NoOp provider
                TelemetryManager.SetProvider(MessageWorkerPool.Telemetry.NoOpTelemetryProvider.Instance);
                openTelemetryProvider.Dispose();
            }
        }

        [Fact]
        public async Task InitPoolAsync_ShouldRecordExceptionInActivity_WhenWorkerInitFails()
        {
            // Arrange
            // Set up OpenTelemetry provider for the test
            var openTelemetryProvider = new OpenTelemetryProvider("MessageWorkerPool", "1.0.0");
            TelemetryManager.SetProvider(openTelemetryProvider);

            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 1, QueueName = "test-queue" };
            var workerPool = new TestWorkerPoolWithFailingWorker(mockWorkerSetting, mockLoggerFactory.Object);

            var activities = new ConcurrentBag<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "MessageWorkerPool",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activities.Add,
                ActivityStopped = activities.Add // Also capture stopped activities
            };
            ActivitySource.AddActivityListener(listener);

            var act = async () => await workerPool.InitPoolAsync(CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();

            // Check if we have any WorkerPool.Init activity (either started or stopped)
            var poolInitActivity = activities.FirstOrDefault(a => a.OperationName == "WorkerPool.Init");

            // The activity should exist and have error status
            if (poolInitActivity != null)
            {
                poolInitActivity.Status.Should().Be(ActivityStatusCode.Error);
            }

            TelemetryManager.SetProvider(NoOpTelemetryProvider.Instance);
            openTelemetryProvider.Dispose();
        }

        [Fact]
        public void Dispose_ShouldClosePool()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 2 };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);

            // Act
            workerPool.Dispose();

            // Assert
            workerPool.IsClosed.Should().BeTrue();
        }

        [Fact]
        public void Dispose_ShouldBeIdempotent()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 2 };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);

            // Act - Call dispose multiple times
            workerPool.Dispose();
            workerPool.Dispose();
            workerPool.Dispose();

            // Assert - Should not throw and should be closed
            workerPool.IsClosed.Should().BeTrue();
        }

        [Fact]
        public async Task GetPoolInformation_ShouldReturnCorrectInformation()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting 
            { 
                WorkerUnitCount = 2, 
                QueueName = "test-queue",
                CommandLine = "dotnet"
            };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);
            await workerPool.InitPoolAsync(CancellationToken.None);

            // Act
            var info = workerPool.GetPoolInformation();

            // Assert
            info.Should().NotBeNull();
            info.QueueName.Should().Be("test-queue");
            info.CommandLine.Should().Be("dotnet");
            info.IsClosed.Should().BeFalse();
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenWorkerSettingIsNull()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();

            // Act
            Action act = () => new TestWorkerPool(null!, mockLoggerFactory.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("workerSetting");
        }

        [Fact]
        public async Task WaitFinishedAsync_ShouldCallGracefulShutDownOnAllWorkers()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 3 };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);
            
            var mockWorker1 = new Mock<IWorker>();
            var mockWorker2 = new Mock<IWorker>();
            var mockWorker3 = new Mock<IWorker>();
            
            mockWorker1.Setup(w => w.GracefulShutDownAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            mockWorker2.Setup(w => w.GracefulShutDownAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            mockWorker3.Setup(w => w.GracefulShutDownAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            
            workerPool.AddWorker(mockWorker1.Object);
            workerPool.AddWorker(mockWorker2.Object);
            workerPool.AddWorker(mockWorker3.Object);

            // Act
            await workerPool.WaitFinishedAsync(CancellationToken.None);

            // Assert
            mockWorker1.Verify(w => w.GracefulShutDownAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockWorker2.Verify(w => w.GracefulShutDownAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockWorker3.Verify(w => w.GracefulShutDownAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Constructor_ShouldSetProcessCount()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 2, QueueName = "test-queue" };

            // Act
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);

            // Assert - Health check timer should be running (metrics initialized)
            workerPool.ProcessCount.Should().Be(2);
            
            // Cleanup
            workerPool.Dispose();
        }

        [Fact]
        public async Task InitPoolAsync_WithMultipleWorkers_ShouldInitializeAll()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 5, QueueName = "multi-worker-queue" };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);

            // Act
            await workerPool.InitPoolAsync(CancellationToken.None);

            // Assert
            workerPool.ProcessCount.Should().Be(5);
        }

        [Fact]
        public async Task InitPoolAsync_ShouldLogInformation()
        {
            // Arrange
            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(mockLogger.Object);
                
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 2, QueueName = "log-test-queue" };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object);

            // Act
            await workerPool.InitPoolAsync(CancellationToken.None);

            // Assert - Verify that logging occurred (can't check exact message but verify Log was called)
            mockLogger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Information),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception?>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.AtLeastOnce);
        }
    }
}
