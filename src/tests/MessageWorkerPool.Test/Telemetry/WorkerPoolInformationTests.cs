using FluentAssertions;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Moq;

namespace MessageWorkerPool.Test.Telemetry
{
    public class WorkerPoolInformationTests
    {
        [Fact]
        public void HealthPercentage_WithHealthyWorkers_ShouldCalculateCorrectly()
        {
            // Arrange
            var info = new WorkerPoolInformation
            {
                TotalWorkers = 10,
                HealthyWorkers = 8
            };

            // Act
            var percentage = info.HealthPercentage;

            // Assert
            percentage.Should().Be(80.0);
        }

        [Fact]
        public void HealthPercentage_WithNoTotalWorkers_ShouldReturnZero()
        {
            // Arrange
            var info = new WorkerPoolInformation
            {
                TotalWorkers = 0,
                HealthyWorkers = 0
            };

            // Act
            var percentage = info.HealthPercentage;

            // Assert
            percentage.Should().Be(0);
        }

        [Fact]
        public void HealthPercentage_WithAllHealthyWorkers_ShouldReturn100()
        {
            // Arrange
            var info = new WorkerPoolInformation
            {
                TotalWorkers = 5,
                HealthyWorkers = 5
            };

            // Act
            var percentage = info.HealthPercentage;

            // Assert
            percentage.Should().Be(100.0);
        }

        [Fact]
        public void HealthPercentage_WithNoHealthyWorkers_ShouldReturnZero()
        {
            // Arrange
            var info = new WorkerPoolInformation
            {
                TotalWorkers = 5,
                HealthyWorkers = 0
            };

            // Act
            var percentage = info.HealthPercentage;

            // Assert
            percentage.Should().Be(0);
        }

        [Fact]
        public void Constructor_ShouldInitializeWorkersListAsEmpty()
        {
            // Arrange & Act
            var info = new WorkerPoolInformation();

            // Assert
            info.Workers.Should().NotBeNull();
            info.Workers.Should().BeEmpty();
        }

        [Fact]
        public void Properties_ShouldBeSettableAndGettable()
        {
            // Arrange
            var createdAt = DateTime.UtcNow;
            var info = new WorkerPoolInformation
            {
                PoolId = "pool-123",
                QueueName = "test-queue",
                TotalWorkers = 5,
                HealthyWorkers = 4,
                StoppedWorkers = 1,
                StoppingWorkers = 0,
                WaitingWorkers = 0,
                CommandLine = "dotnet worker.dll",
                CreatedAt = createdAt,
                IsClosed = false
            };

            // Assert
            info.PoolId.Should().Be("pool-123");
            info.QueueName.Should().Be("test-queue");
            info.TotalWorkers.Should().Be(5);
            info.HealthyWorkers.Should().Be(4);
            info.StoppedWorkers.Should().Be(1);
            info.StoppingWorkers.Should().Be(0);
            info.WaitingWorkers.Should().Be(0);
            info.CommandLine.Should().Be("dotnet worker.dll");
            info.CreatedAt.Should().Be(createdAt);
            info.IsClosed.Should().BeFalse();
        }
    }

    public class WorkerInformationTests
    {
        [Fact]
        public void IsHealthy_WithRunningStatus_ShouldReturnTrue()
        {
            // Arrange
            var info = new WorkerInformation
            {
                Status = WorkerStatus.Running
            };

            // Act & Assert
            info.IsHealthy.Should().BeTrue();
        }

        [Fact]
        public void IsHealthy_WithStoppedStatus_ShouldReturnFalse()
        {
            // Arrange
            var info = new WorkerInformation
            {
                Status = WorkerStatus.Stopped
            };

            // Act & Assert
            info.IsHealthy.Should().BeFalse();
        }

        [Fact]
        public void IsHealthy_WithStoppingStatus_ShouldReturnFalse()
        {
            // Arrange
            var info = new WorkerInformation
            {
                Status = WorkerStatus.Stopping
            };

            // Act & Assert
            info.IsHealthy.Should().BeFalse();
        }

        [Fact]
        public void IsHealthy_WithWaitForInitStatus_ShouldReturnFalse()
        {
            // Arrange
            var info = new WorkerInformation
            {
                Status = WorkerStatus.WaitForInit
            };

            // Act & Assert
            info.IsHealthy.Should().BeFalse();
        }

        [Fact]
        public void Properties_ShouldBeSettableAndGettable()
        {
            // Arrange
            var createdAt = DateTime.UtcNow;
            var lastActivityAt = DateTime.UtcNow.AddMinutes(-5);
            var info = new WorkerInformation
            {
                WorkerId = "worker-1",
                ProcessId = 12345,
                Status = WorkerStatus.Running,
                QueueName = "test-queue",
                TasksProcessed = 100,
                CurrentTaskCount = 2,
                CreatedAt = createdAt,
                LastActivityAt = lastActivityAt
            };

            // Assert
            info.WorkerId.Should().Be("worker-1");
            info.ProcessId.Should().Be(12345);
            info.Status.Should().Be(WorkerStatus.Running);
            info.QueueName.Should().Be("test-queue");
            info.TasksProcessed.Should().Be(100);
            info.CurrentTaskCount.Should().Be(2);
            info.CreatedAt.Should().Be(createdAt);
            info.LastActivityAt.Should().Be(lastActivityAt);
        }
    }

    public class WorkerPoolInformationCollectorTests
    {
        [Fact]
        public void CollectWorkerStatus_WithRunningWorkers_ShouldCountCorrectly()
        {
            // Arrange
            var workers = new List<IWorker>
            {
                new TestWorker(WorkerStatus.Running),
                new TestWorker(WorkerStatus.Running),
                new TestWorker(WorkerStatus.Running)
            };

            // Act
            var summary = WorkerPoolInformationCollector.CollectWorkerStatus(workers, "test-queue");

            // Assert
            summary.QueueName.Should().Be("test-queue");
            summary.TotalWorkers.Should().Be(3);
            summary.HealthyWorkers.Should().Be(3);
            summary.StoppedWorkers.Should().Be(0);
            summary.StoppingWorkers.Should().Be(0);
            summary.WaitingWorkers.Should().Be(0);
        }

        [Fact]
        public void CollectWorkerStatus_WithMixedStatuses_ShouldCountCorrectly()
        {
            // Arrange
            var workers = new List<IWorker>
            {
                new TestWorker(WorkerStatus.Running),
                new TestWorker(WorkerStatus.Running),
                new TestWorker(WorkerStatus.Stopped),
                new TestWorker(WorkerStatus.Stopping),
                new TestWorker(WorkerStatus.WaitForInit)
            };

            // Act
            var summary = WorkerPoolInformationCollector.CollectWorkerStatus(workers, "test-queue");

            // Assert
            summary.TotalWorkers.Should().Be(5);
            summary.HealthyWorkers.Should().Be(2);
            summary.StoppedWorkers.Should().Be(1);
            summary.StoppingWorkers.Should().Be(1);
            summary.WaitingWorkers.Should().Be(1);
        }

        [Fact]
        public void CollectWorkerStatus_WithEmptyWorkerList_ShouldReturnZeroCounts()
        {
            // Arrange
            var workers = new List<IWorker>();

            // Act
            var summary = WorkerPoolInformationCollector.CollectWorkerStatus(workers, "test-queue");

            // Assert
            summary.TotalWorkers.Should().Be(0);
            summary.HealthyWorkers.Should().Be(0);
            summary.StoppedWorkers.Should().Be(0);
            summary.StoppingWorkers.Should().Be(0);
            summary.WaitingWorkers.Should().Be(0);
        }

        [Fact]
        public void CollectWorkerStatus_WithNonWorkerBaseInstances_ShouldNotCount()
        {
            // Arrange
            var mockWorker = new Mock<IWorker>();
            var workers = new List<IWorker> { mockWorker.Object };

            // Act
            var summary = WorkerPoolInformationCollector.CollectWorkerStatus(workers, "test-queue");

            // Assert
            summary.TotalWorkers.Should().Be(1);
            summary.HealthyWorkers.Should().Be(0);
            summary.StoppedWorkers.Should().Be(0);
            summary.StoppingWorkers.Should().Be(0);
            summary.WaitingWorkers.Should().Be(0);
        }

        [Fact]
        public void CollectWorkerStatus_WithAllStoppedWorkers_ShouldCountCorrectly()
        {
            // Arrange
            var workers = new List<IWorker>
            {
                new TestWorker(WorkerStatus.Stopped),
                new TestWorker(WorkerStatus.Stopped),
                new TestWorker(WorkerStatus.Stopped)
            };

            // Act
            var summary = WorkerPoolInformationCollector.CollectWorkerStatus(workers, "test-queue");

            // Assert
            summary.TotalWorkers.Should().Be(3);
            summary.HealthyWorkers.Should().Be(0);
            summary.StoppedWorkers.Should().Be(3);
            summary.StoppingWorkers.Should().Be(0);
            summary.WaitingWorkers.Should().Be(0);
        }

        [Fact]
        public void CollectWorkerStatus_WithAllWaitingWorkers_ShouldCountCorrectly()
        {
            // Arrange
            var workers = new List<IWorker>
            {
                new TestWorker(WorkerStatus.WaitForInit),
                new TestWorker(WorkerStatus.WaitForInit)
            };

            // Act
            var summary = WorkerPoolInformationCollector.CollectWorkerStatus(workers, "test-queue");

            // Assert
            summary.TotalWorkers.Should().Be(2);
            summary.HealthyWorkers.Should().Be(0);
            summary.StoppedWorkers.Should().Be(0);
            summary.StoppingWorkers.Should().Be(0);
            summary.WaitingWorkers.Should().Be(2);
        }

        [Fact]
        public void CollectWorkerStatus_WithDifferentQueueName_ShouldSetCorrectly()
        {
            // Arrange
            var workers = new List<IWorker> { new TestWorker(WorkerStatus.Running) };

            // Act
            var summary = WorkerPoolInformationCollector.CollectWorkerStatus(workers, "my-queue");

            // Assert
            summary.QueueName.Should().Be("my-queue");
        }

        // Test helper class that extends WorkerBase and allows us to set status
        private class TestWorker : WorkerBase
        {
            private readonly WorkerStatus _testStatus;

            public TestWorker(WorkerStatus status) 
                : base(new WorkerPoolSetting { QueueName = "test", CommandLine = "test" }, 
                       Mock.Of<Microsoft.Extensions.Logging.ILogger<WorkerBase>>())
            {
                _testStatus = status;
                // Use reflection to set the private _status field
                var statusField = typeof(WorkerBase).GetField("_status", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                statusField?.SetValue(this, status);
            }

            protected override void SetupMessageQueueSetting(System.Threading.CancellationToken token)
            {
                // Not needed for tests
            }
        }
    }

    public class WorkerStatusSummaryTests
    {
        [Fact]
        public void Properties_ShouldBeSettableAndGettable()
        {
            // Arrange & Act
            var summary = new WorkerStatusSummary
            {
                QueueName = "test-queue",
                TotalWorkers = 10,
                HealthyWorkers = 8,
                StoppedWorkers = 1,
                StoppingWorkers = 1,
                WaitingWorkers = 0
            };

            // Assert
            summary.QueueName.Should().Be("test-queue");
            summary.TotalWorkers.Should().Be(10);
            summary.HealthyWorkers.Should().Be(8);
            summary.StoppedWorkers.Should().Be(1);
            summary.StoppingWorkers.Should().Be(1);
            summary.WaitingWorkers.Should().Be(0);
        }

        [Fact]
        public void DefaultConstructor_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var summary = new WorkerStatusSummary();

            // Assert
            summary.QueueName.Should().BeNull();
            summary.TotalWorkers.Should().Be(0);
            summary.HealthyWorkers.Should().Be(0);
            summary.StoppedWorkers.Should().Be(0);
            summary.StoppingWorkers.Should().Be(0);
            summary.WaitingWorkers.Should().Be(0);
        }
    }
}
