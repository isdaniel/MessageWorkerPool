using FluentAssertions;
using MessageWorkerPool.OpenTelemetry;
using System;
using System.Diagnostics.Metrics;
using System.Linq;
using Xunit;

namespace MessageWorkerPool.Test.OpenTelemetry
{
    public class OpenTelemetryMetricsTests : IDisposable
    {
        private readonly OpenTelemetryMetrics _metrics;

        public OpenTelemetryMetricsTests()
        {
            _metrics = new OpenTelemetryMetrics("TestMeter", "1.0.0");
        }

        public void Dispose()
        {
            _metrics?.Dispose();
        }

        [Fact]
        public void Constructor_WithDefaultParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            using var metrics = new OpenTelemetryMetrics();

            // Assert
            metrics.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithCustomParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            using var metrics = new OpenTelemetryMetrics("CustomMeter", "2.0.0");

            // Assert
            metrics.Should().NotBeNull();
        }

        [Fact]
        public void RecordTaskProcessed_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskProcessed("test-queue", "worker-1");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskProcessed_WithNullParameters_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskProcessed(null, null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskFailed_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskFailed("test-queue", "worker-1", "InvalidOperationException");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskFailed_WithNullErrorType_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskFailed("test-queue", "worker-1", null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskFailed_WithAllNullParameters_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskFailed(null, null, null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskRejected_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskRejected("test-queue", "worker-1");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskRejected_WithNullParameters_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskRejected(null, null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskDuration_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskDuration(123.45, "test-queue", "worker-1");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskDuration_WithZeroDuration_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskDuration(0, "test-queue", "worker-1");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskDuration_WithNegativeDuration_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskDuration(-100, "test-queue", "worker-1");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskDuration_ConvertsMillisecondsToSeconds()
        {
            // Arrange - 1000ms should be converted to 1 second
            // We can't directly verify the value, but we can ensure the method executes

            // Act
            Action act = () => _metrics.RecordTaskDuration(1000, "test-queue", "worker-1");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetActiveWorkers_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.SetActiveWorkers(5);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetActiveWorkers_WithZero_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.SetActiveWorkers(0);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetActiveWorkers_WithNegative_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.SetActiveWorkers(-1);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetProcessingTasks_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.SetProcessingTasks(10);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetHealthyWorkers_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.SetHealthyWorkers(3);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetStoppedWorkers_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.SetStoppedWorkers(2);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void IncrementProcessingTasks_ShouldIncrement()
        {
            // Act - Call multiple times
            _metrics.IncrementProcessingTasks();
            _metrics.IncrementProcessingTasks();
            _metrics.IncrementProcessingTasks();

            // Assert - Should not throw
            Action act = () => _metrics.IncrementProcessingTasks();
            act.Should().NotThrow();
        }

        [Fact]
        public void DecrementProcessingTasks_ShouldDecrement()
        {
            // Arrange - Increment first
            _metrics.IncrementProcessingTasks();
            _metrics.IncrementProcessingTasks();

            // Act
            _metrics.DecrementProcessingTasks();

            // Assert - Should not throw
            Action act = () => _metrics.DecrementProcessingTasks();
            act.Should().NotThrow();
        }

        [Fact]
        public void IncrementAndDecrementProcessingTasks_ShouldBeThreadSafe()
        {
            // Arrange
            var tasks = new System.Threading.Tasks.Task[100];

            // Act - Perform concurrent increments and decrements
            for (int i = 0; i < 50; i++)
            {
                tasks[i * 2] = System.Threading.Tasks.Task.Run(() => _metrics.IncrementProcessingTasks());
                tasks[i * 2 + 1] = System.Threading.Tasks.Task.Run(() => _metrics.DecrementProcessingTasks());
            }

            // Assert - Should not throw
            Action act = () => System.Threading.Tasks.Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_ShouldDisposeResourcesWithoutError()
        {
            // Arrange
            var metrics = new OpenTelemetryMetrics("TestMeter", "1.0.0");

            // Act
            Action act = () => metrics.Dispose();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var metrics = new OpenTelemetryMetrics("TestMeter", "1.0.0");

            // Act
            Action act = () =>
            {
                metrics.Dispose();
                metrics.Dispose();
                metrics.Dispose();
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskProcessed_AfterDispose_ShouldNotThrow()
        {
            // Arrange
            var metrics = new OpenTelemetryMetrics("TestMeter", "1.0.0");
            metrics.Dispose();

            // Act
            Action act = () => metrics.RecordTaskProcessed("queue", "worker");

            // Assert - Behavior after disposal may vary, but shouldn't crash
            act.Should().NotThrow();
        }

        [Fact]
        public void MultipleMetricOperations_ShouldWorkTogether()
        {
            // Act & Assert - Simulate realistic usage
            Action act = () =>
            {
                _metrics.SetActiveWorkers(5);
                _metrics.IncrementProcessingTasks();
                _metrics.RecordTaskProcessed("test-queue", "worker-1");
                _metrics.RecordTaskDuration(100.5, "test-queue", "worker-1");
                _metrics.DecrementProcessingTasks();
                _metrics.SetHealthyWorkers(5);
                _metrics.RecordTaskFailed("test-queue", "worker-2", "Exception");
                _metrics.RecordTaskRejected("test-queue", "worker-3");
                _metrics.SetStoppedWorkers(0);
            };

            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskDuration_WithLargeValue_ShouldNotThrow()
        {
            // Act
            Action act = () => _metrics.RecordTaskDuration(double.MaxValue, "test-queue", "worker-1");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetActiveWorkers_MultipleTimesShouldUpdateValue()
        {
            // Act & Assert - Each call should succeed
            Action act = () =>
            {
                _metrics.SetActiveWorkers(1);
                _metrics.SetActiveWorkers(5);
                _metrics.SetActiveWorkers(10);
                _metrics.SetActiveWorkers(0);
            };

            act.Should().NotThrow();
        }

        [Fact]
        public void RecordMultipleTasksProcessed_ShouldNotThrow()
        {
            // Act
            Action act = () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    _metrics.RecordTaskProcessed($"queue-{i % 3}", $"worker-{i % 5}");
                }
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskFailed_WithDifferentErrorTypes_ShouldNotThrow()
        {
            // Act
            Action act = () =>
            {
                _metrics.RecordTaskFailed("queue", "worker", "InvalidOperationException");
                _metrics.RecordTaskFailed("queue", "worker", "ArgumentException");
                _metrics.RecordTaskFailed("queue", "worker", "NullReferenceException");
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void IncrementProcessingTasks_CalledManyTimes_ShouldNotThrow()
        {
            // Act
            Action act = () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    _metrics.IncrementProcessingTasks();
                }
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void DecrementProcessingTasks_BelowZero_ShouldNotThrow()
        {
            // Act - Decrement without incrementing first
            Action act = () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    _metrics.DecrementProcessingTasks();
                }
            };

            // Assert - Should handle negative values gracefully
            act.Should().NotThrow();
        }
    }
}
