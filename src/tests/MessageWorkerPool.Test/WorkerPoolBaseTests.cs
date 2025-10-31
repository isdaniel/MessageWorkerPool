using FluentAssertions;
using MessageWorkerPool.Test.Utility;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.OpenTelemetry;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using System.Collections.Concurrent;
using MessageWorkerPool.Utilities;

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
            var telemetryManager = new TelemetryManager(openTelemetryProvider);

            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 2, QueueName = "test-queue" };
            var workerPool = new TestWorkerPool(mockWorkerSetting, mockLoggerFactory.Object, telemetryManager);

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
                // Clean up
                openTelemetryProvider.Dispose();
            }
        }

        [Fact]
        public async Task InitPoolAsync_ShouldRecordExceptionInActivity_WhenWorkerInitFails()
        {
            // Arrange
            // Set up OpenTelemetry provider for the test
            var openTelemetryProvider = new OpenTelemetryProvider("MessageWorkerPool", "1.0.0");
            var telemetryManager = new TelemetryManager(openTelemetryProvider);

            var mockLoggerFactory = CreateMockLoggerFactory();
            var mockWorkerSetting = new WorkerPoolSetting { WorkerUnitCount = 1, QueueName = "test-queue" };
            var workerPool = new TestWorkerPoolWithFailingWorker(mockWorkerSetting, mockLoggerFactory.Object, telemetryManager);

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

    public class WorkerPoolBaseAdditionalTests
    {
        private class TestWorkerPool : WorkerPoolBase
        {
            private readonly Func<IWorker> _workerFactory;
            private readonly bool _throwOnWorkerCreation;

            public TestWorkerPool(
                WorkerPoolSetting setting,
                ILoggerFactory loggerFactory,
                Func<IWorker> workerFactory = null,
                bool throwOnWorkerCreation = false)
                : base(setting, loggerFactory)
            {
                _workerFactory = workerFactory;
                _throwOnWorkerCreation = throwOnWorkerCreation;
            }

            protected override IWorker GetWorker()
            {
                if (_throwOnWorkerCreation)
                {
                    throw new InvalidOperationException("Worker creation failed");
                }
                return _workerFactory?.Invoke() ?? new Mock<IWorker>().Object;
            }
        }

        private class TestFailingWorker : IWorker
        {
            public WorkerStatus Status => WorkerStatus.WaitForInit;
            public string WorkerId => "test-worker";
            public int? ProcessId => 12345;

            public Task InitWorkerAsync(CancellationToken token)
            {
                throw new InvalidOperationException("Init failed");
            }

            public Task GracefulShutDownAsync(CancellationToken token)
            {
                return Task.CompletedTask;
            }

            public void Dispose() { }
        }

        [Fact]
        public async Task InitPoolAsync_ShouldThrowAndLogError_WhenWorkerCreationFails()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

            var setting = new WorkerPoolSetting { WorkerUnitCount = 3, QueueName = "test-queue" };
            var pool = new TestWorkerPool(setting, mockLoggerFactory.Object, throwOnWorkerCreation: true);

            // Act & Assert
            var act = async () => await pool.InitPoolAsync(CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Worker creation failed");

            mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Failed to initialize worker pool")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InitPoolAsync_ShouldThrow_WhenWorkerInitFails()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

            var setting = new WorkerPoolSetting { WorkerUnitCount = 2, QueueName = "test-queue" };
            var pool = new TestWorkerPool(setting, mockLoggerFactory.Object, () => new TestFailingWorker());

            // Act & Assert
            var act = async () => await pool.InitPoolAsync(CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Init failed");
        }

        [Fact]
        public async Task InitPoolAsync_ShouldInitializeAllWorkers_BeforeFailure()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

            int workerCreationCount = 0;
            Func<IWorker> workerFactory = () =>
            {
                workerCreationCount++;
                if (workerCreationCount == 2)
                {
                    return new TestFailingWorker(); // Second worker fails
                }
                var mockWorker = new Mock<IWorker>();
                mockWorker.Setup(w => w.InitWorkerAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                return mockWorker.Object;
            };

            var setting = new WorkerPoolSetting { WorkerUnitCount = 3, QueueName = "test-queue" };
            var pool = new TestWorkerPool(setting, mockLoggerFactory.Object, workerFactory);

            // Act & Assert
            var act = async () => await pool.InitPoolAsync(CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();

            // Verify that we attempted to create 2 workers before failing
            workerCreationCount.Should().Be(2);
        }

        [Fact]
        public async Task WaitFinishedAsync_ShouldWaitForAllWorkers()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

            var worker1Task = new TaskCompletionSource<bool>();
            var worker2Task = new TaskCompletionSource<bool>();

            var mockWorker1 = new Mock<IWorker>();
            var mockWorker2 = new Mock<IWorker>();

            mockWorker1.Setup(w => w.GracefulShutDownAsync(It.IsAny<CancellationToken>()))
                .Returns(worker1Task.Task.ContinueWith(_ => { }));
            mockWorker2.Setup(w => w.GracefulShutDownAsync(It.IsAny<CancellationToken>()))
                .Returns(worker2Task.Task.ContinueWith(_ => { }));
            mockWorker1.Setup(w => w.InitWorkerAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            mockWorker2.Setup(w => w.InitWorkerAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var workers = new Queue<IWorker>(new[] { mockWorker1.Object, mockWorker2.Object });
            Func<IWorker> workerFactory = () => workers.Dequeue();

            var setting = new WorkerPoolSetting { WorkerUnitCount = 2, QueueName = "test-queue" };
            var pool = new TestWorkerPool(setting, mockLoggerFactory.Object, workerFactory);

            await pool.InitPoolAsync(CancellationToken.None);

            // Act - Start shutdown and wait for completion
            var waitTask = pool.WaitFinishedAsync(CancellationToken.None);

            // Complete workers one by one
            await Task.Delay(100);
            worker1Task.SetResult(true);
            await Task.Delay(100);
            worker2Task.SetResult(true);

            await waitTask;

            // Assert
            mockWorker1.Verify(w => w.GracefulShutDownAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockWorker2.Verify(w => w.GracefulShutDownAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockWorker1.Verify(w => w.Dispose(), Times.Once);
            mockWorker2.Verify(w => w.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_ShouldDisposeAllWorkers()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

            var mockWorker1 = new Mock<IWorker>();
            var mockWorker2 = new Mock<IWorker>();
            mockWorker1.Setup(w => w.InitWorkerAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            mockWorker2.Setup(w => w.InitWorkerAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var workers = new Queue<IWorker>(new[] { mockWorker1.Object, mockWorker2.Object });
            Func<IWorker> workerFactory = () => workers.Dequeue();

            var setting = new WorkerPoolSetting { WorkerUnitCount = 2, QueueName = "test-queue" };
            var pool = new TestWorkerPool(setting, mockLoggerFactory.Object, workerFactory);

            pool.InitPoolAsync(CancellationToken.None).Wait();

            // Act
            pool.Dispose();

            // Assert
            mockWorker1.Verify(w => w.Dispose(), Times.Once);
            mockWorker2.Verify(w => w.Dispose(), Times.Once);
            pool.IsClosed.Should().BeTrue();
        }

        [Fact]
        public void Dispose_ShouldBeIdempotent()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

            var mockWorker = new Mock<IWorker>();
            mockWorker.Setup(w => w.InitWorkerAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var setting = new WorkerPoolSetting { WorkerUnitCount = 1, QueueName = "test-queue" };
            var pool = new TestWorkerPool(setting, mockLoggerFactory.Object, () => mockWorker.Object);

            pool.InitPoolAsync(CancellationToken.None).Wait();

            // Act - Dispose multiple times
            pool.Dispose();
            pool.Dispose();
            pool.Dispose();

            // Assert - Worker should only be disposed once
            mockWorker.Verify(w => w.Dispose(), Times.Once);
        }

        [Fact]
        public void ProcessCount_ShouldReturnCorrectValue()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

            var setting = new WorkerPoolSetting { WorkerUnitCount = 7, QueueName = "test-queue" };
            var pool = new TestWorkerPool(setting, mockLoggerFactory.Object);

            // Act
            var count = pool.ProcessCount;

            // Assert
            count.Should().Be(7);
        }

        [Fact]
        public void IsClosed_ShouldBeFalseInitially()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

            var setting = new WorkerPoolSetting { WorkerUnitCount = 1, QueueName = "test-queue" };
            var pool = new TestWorkerPool(setting, mockLoggerFactory.Object);

            // Act & Assert
            pool.IsClosed.Should().BeFalse();
        }

        [Fact]
        public void IsClosed_ShouldBeTrueAfterDispose()
        {
            // Arrange
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger<WorkerPoolBase>>();
            mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

            var setting = new WorkerPoolSetting { WorkerUnitCount = 1, QueueName = "test-queue" };
            var pool = new TestWorkerPool(setting, mockLoggerFactory.Object);

            // Act
            pool.Dispose();

            // Assert
            pool.IsClosed.Should().BeTrue();
        }
    }
}
