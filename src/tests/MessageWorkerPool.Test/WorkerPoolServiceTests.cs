using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessageWorkerPool.Test
{
    public class WorkerPoolServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldInitializeWorkerPools()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<WorkerPoolService>>();
            var mockWorkerPoolFactory = new Mock<IWorkerPoolFactory>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockWorkerPool = new Mock<IWorkerPool>();
            var workerSettings = new[] { new WorkerPoolSetting(), new WorkerPoolSetting() };

            mockWorkerPool.Setup(pool => pool.InitPoolAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockWorkerPoolFactory.Setup(factory => factory.CreateWorkerPool(It.IsAny<WorkerPoolSetting>()))
                .Returns(mockWorkerPool.Object);

            var service = new WorkerPoolService(workerSettings, mockWorkerPoolFactory.Object, mockLoggerFactory.Object, mockLogger.Object);

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert
            mockWorkerPoolFactory.Verify(factory => factory.CreateWorkerPool(It.IsAny<WorkerPoolSetting>()), Times.Exactly(workerSettings.Length));
            mockWorkerPool.Verify(pool => pool.InitPoolAsync(It.IsAny<CancellationToken>()), Times.Exactly(workerSettings.Length));
            mockLogger.Verify(logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == "WorkerPool initialization Finish!"),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ), Times.Once);
        }

        [Fact]
        public async Task StopAsync_ShouldWaitForWorkerPoolsToFinish()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<WorkerPoolService>>();
            var mockWorkerPoolFactory = new Mock<IWorkerPoolFactory>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockWorkerPool = new Mock<IWorkerPool>();
            var workerSettings = new[] { new WorkerPoolSetting(), new WorkerPoolSetting() };

            mockWorkerPool.Setup(pool => pool.InitPoolAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockWorkerPool.Setup(pool => pool.WaitFinishedAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockWorkerPoolFactory.Setup(factory => factory.CreateWorkerPool(It.IsAny<WorkerPoolSetting>()))
                .Returns(mockWorkerPool.Object);

            var service = new WorkerPoolService(workerSettings, mockWorkerPoolFactory.Object, mockLoggerFactory.Object, mockLogger.Object);

            await service.StartAsync(CancellationToken.None);

            // Act
            await service.StopAsync(CancellationToken.None);

            // Assert
            mockWorkerPool.Verify(pool => pool.WaitFinishedAsync(It.IsAny<CancellationToken>()), Times.Exactly(workerSettings.Length));
            mockLogger.Verify(logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Start Stop")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ), Times.Once);
            mockLogger.Verify(logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Stop Service")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldComplete_WhenNoWorkerSettingsProvided()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<WorkerPoolService>>();
            var mockWorkerPoolFactory = new Mock<IWorkerPoolFactory>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var workerSettings = Array.Empty<WorkerPoolSetting>();

            var service = new WorkerPoolService(workerSettings, mockWorkerPoolFactory.Object, mockLoggerFactory.Object, mockLogger.Object);

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert
            mockWorkerPoolFactory.Verify(factory => factory.CreateWorkerPool(It.IsAny<WorkerPoolSetting>()), Times.Never);
            mockLogger.Verify(logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == "WorkerPool initialization Finish!"),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ), Times.Once);
        }

        [Fact]
        public async Task StopAsync_ShouldComplete_WhenNoWorkerPoolsInitialized()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<WorkerPoolService>>();
            var mockWorkerPoolFactory = new Mock<IWorkerPoolFactory>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var workerSettings = Array.Empty<WorkerPoolSetting>();

            var service = new WorkerPoolService(workerSettings, mockWorkerPoolFactory.Object, mockLoggerFactory.Object, mockLogger.Object);

            // Act
            await service.StopAsync(CancellationToken.None);

            // Assert
            mockLogger.Verify(logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Start Stop")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ), Times.Once);
            mockLogger.Verify(logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Stop Service")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldLogError_WhenWorkerPoolInitializationFails()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<WorkerPoolService>>();
            var mockWorkerPoolFactory = new Mock<IWorkerPoolFactory>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockWorkerPool = new Mock<IWorkerPool>();
            var workerSettings = new[] { new WorkerPoolSetting() };

            mockWorkerPool.Setup(pool => pool.InitPoolAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Initialization failed"));
            mockWorkerPoolFactory.Setup(factory => factory.CreateWorkerPool(It.IsAny<WorkerPoolSetting>()))
                .Returns(mockWorkerPool.Object);

            var service = new WorkerPoolService(workerSettings, mockWorkerPoolFactory.Object, mockLoggerFactory.Object, mockLogger.Object);

            // Act
            var task = async () => await service.StartAsync(CancellationToken.None);
            await task.Should().ThrowAsync<InvalidOperationException>();

            // Assert
            mockLogger.Verify(logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Initialization failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<WorkerPoolService>>();
            var mockWorkerPoolFactory = new Mock<IWorkerPoolFactory>();
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockWorkerPool = new Mock<IWorkerPool>();
            var workerSettings = new[] { new WorkerPoolSetting(), new WorkerPoolSetting() };

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel(); // Immediately cancel

            mockWorkerPool.Setup(pool => pool.InitPoolAsync(It.IsAny<CancellationToken>()))
                .Returns(async (CancellationToken token) =>
                {
                    await Task.Delay(100, token); // Simulate work
                });
            mockWorkerPoolFactory.Setup(factory => factory.CreateWorkerPool(It.IsAny<WorkerPoolSetting>()))
                .Returns(mockWorkerPool.Object);

            var service = new WorkerPoolService(workerSettings, mockWorkerPoolFactory.Object, mockLoggerFactory.Object, mockLogger.Object);

            // Act
            await service.StartAsync(cancellationTokenSource.Token);

            // Assert
            mockWorkerPool.Verify(pool => pool.InitPoolAsync(It.IsAny<CancellationToken>()), Times.Never);
            mockLogger.Verify(logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == "WorkerPool initialization Finish!"),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ), Times.Never);
        }
    }
}
