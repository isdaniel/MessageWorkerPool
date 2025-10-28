using FluentAssertions;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.Telemetry.Abstractions;
using MessageWorkerPool.Utilities;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MessageWorkerPool.Test.Telemetry
{
    public class NoOpTelemetryProviderTests
    {
        [Fact]
        public void Instance_ShouldReturnSingletonInstance()
        {
            // Act
            var instance1 = NoOpTelemetryProvider.Instance;
            var instance2 = NoOpTelemetryProvider.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [Fact]
        public void StartActivity_ShouldReturnNoOpActivity()
        {
            // Arrange
            var provider = NoOpTelemetryProvider.Instance;

            // Act
            var activity = provider.StartActivity("test-operation");

            // Assert
            activity.Should().NotBeNull();
            activity.Should().BeOfType<NoOpActivity>();
        }

        [Fact]
        public void StartActivity_WithTags_ShouldReturnNoOpActivity()
        {
            // Arrange
            var provider = NoOpTelemetryProvider.Instance;
            var tags = new Dictionary<string, object> { { "key", "value" } };

            // Act
            var activity = provider.StartActivity("test-operation", tags);

            // Assert
            activity.Should().NotBeNull();
            activity.Should().BeOfType<NoOpActivity>();
        }

        [Fact]
        public void Metrics_ShouldReturnNoOpMetrics()
        {
            // Arrange
            var provider = NoOpTelemetryProvider.Instance;

            // Act
            var metrics = provider.Metrics;

            // Assert
            metrics.Should().NotBeNull();
            metrics.Should().BeOfType<NoOpMetrics>();
        }

        [Fact]
        public void Metrics_ShouldReturnSameSingletonInstance()
        {
            // Arrange
            var provider = NoOpTelemetryProvider.Instance;

            // Act
            var metrics1 = provider.Metrics;
            var metrics2 = provider.Metrics;

            // Assert
            metrics1.Should().BeSameAs(metrics2);
        }
    }

    public class NoOpActivityTests
    {
        [Fact]
        public void Instance_ShouldReturnSingletonInstance()
        {
            // Act
            var instance1 = NoOpActivity.Instance;
            var instance2 = NoOpActivity.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [Fact]
        public void SetTag_ShouldNotThrow()
        {
            // Arrange
            var activity = NoOpActivity.Instance;

            // Act
            Action act = () => activity.SetTag("key", "value");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetTags_ShouldNotThrow()
        {
            // Arrange
            var activity = NoOpActivity.Instance;
            var tags = new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } };

            // Act
            Action act = () => activity.SetTags(tags);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetStatus_ShouldNotThrow()
        {
            // Arrange
            var activity = NoOpActivity.Instance;

            // Act
            Action act = () => activity.SetStatus(ActivityStatus.Ok, "description");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordException_ShouldNotThrow()
        {
            // Arrange
            var activity = NoOpActivity.Instance;
            var exception = new InvalidOperationException("test");

            // Act
            Action act = () => activity.RecordException(exception);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Arrange
            var activity = NoOpActivity.Instance;

            // Act
            Action act = () => activity.Dispose();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var activity = NoOpActivity.Instance;

            // Act
            Action act = () =>
            {
                activity.Dispose();
                activity.Dispose();
                activity.Dispose();
            };

            // Assert
            act.Should().NotThrow();
        }
    }

    public class NoOpMetricsTests
    {
        [Fact]
        public void Instance_ShouldReturnSingletonInstance()
        {
            // Act
            var instance1 = NoOpMetrics.Instance;
            var instance2 = NoOpMetrics.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [Fact]
        public void RecordTaskProcessed_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act
            Action act = () => metrics.RecordTaskProcessed("queue", "worker");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskFailed_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act
            Action act = () => metrics.RecordTaskFailed("queue", "worker", "ErrorType");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskRejected_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act
            Action act = () => metrics.RecordTaskRejected("queue", "worker");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordTaskDuration_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act
            Action act = () => metrics.RecordTaskDuration(123.45, "queue", "worker");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetActiveWorkers_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act
            Action act = () => metrics.SetActiveWorkers(5);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetProcessingTasks_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act
            Action act = () => metrics.SetProcessingTasks(10);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetHealthyWorkers_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act
            Action act = () => metrics.SetHealthyWorkers(3);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetStoppedWorkers_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act
            Action act = () => metrics.SetStoppedWorkers(2);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void IncrementProcessingTasks_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act
            Action act = () => metrics.IncrementProcessingTasks();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void DecrementProcessingTasks_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act
            Action act = () => metrics.DecrementProcessingTasks();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void AllMethods_WithNullParameters_ShouldNotThrow()
        {
            // Arrange
            var metrics = NoOpMetrics.Instance;

            // Act & Assert
            Action act = () =>
            {
                metrics.RecordTaskProcessed(null, null);
                metrics.RecordTaskFailed(null, null, null);
                metrics.RecordTaskRejected(null, null);
                metrics.RecordTaskDuration(0, null, null);
            };

            act.Should().NotThrow();
        }
    }
}
