using FluentAssertions;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.Telemetry.Abstractions;
using MessageWorkerPool.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace MessageWorkerPool.Test.Telemetry
{
    [Collection("TelemetryTests")]
    public class TaskProcessingTelemetryTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<ITelemetryProvider> _mockProvider;
        private readonly Mock<IActivity> _mockActivity;
        private readonly Mock<IMetrics> _mockMetrics;

        public TaskProcessingTelemetryTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockProvider = new Mock<ITelemetryProvider>();
            _mockActivity = new Mock<IActivity>();
            _mockMetrics = new Mock<IMetrics>();

            _mockProvider.Setup(p => p.StartActivity(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>()))
                .Returns(_mockActivity.Object);
            _mockProvider.Setup(p => p.Metrics).Returns(_mockMetrics.Object);
            
            TelemetryManager.SetProvider(_mockProvider.Object);
        }

        public void Dispose()
        {
            // Clean up - reset to NoOp provider after each test
            TelemetryManager.SetProvider(NoOpTelemetryProvider.Instance);
        }

        [Fact]
        public void Constructor_ShouldStartStopwatchAndActivity()
        {
            // Arrange & Act
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Assert
            _mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>()), Times.Once);
            _mockMetrics.Verify(m => m.IncrementProcessingTasks(), Times.Once);
        }

        [Fact]
        public void Constructor_ShouldSetActivityTags()
        {
            // Arrange & Act
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Assert
            _mockActivity.Verify(a => a.SetTag("worker.id", "worker-1"), Times.Once);
            _mockActivity.Verify(a => a.SetTag("queue.name", "test-queue"), Times.Once);
            _mockActivity.Verify(a => a.SetTag("correlation.id", "corr-123"), Times.Once);
            _mockActivity.Verify(a => a.SetTag("messaging.system", "rabbitmq"), Times.Once);
            _mockActivity.Verify(a => a.SetTag("messaging.destination", "test-queue"), Times.Once);
        }

        [Fact]
        public void Constructor_WithMessageHeaders_ShouldPassHeadersToActivityCreation()
        {
            // Arrange
            var messageHeaders = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "custom-header", "custom-value" }
            };

            // Act
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object, messageHeaders);

            // Assert
            _mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, It.IsAny<ActivityContext>()), Times.Once);
            _mockMetrics.Verify(m => m.IncrementProcessingTasks(), Times.Once);
        }

        [Fact]
        public void Constructor_WithMessageHeadersAndContextExtractor_ShouldExtractParentContext()
        {
            // Arrange
            var messageHeaders = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };
            
            var expectedContext = new ActivityContext(
                ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()),
                ActivitySpanId.CreateFromString("b7ad6b7169203331".AsSpan()),
                ActivityTraceFlags.Recorded);

            Func<IDictionary<string, object>, ActivityContext> contextExtractor = (headers) => expectedContext;
            TelemetryManager.SetTraceContextExtractor(contextExtractor);

            // Act
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object, messageHeaders);

            // Assert
            _mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, expectedContext), Times.Once);
        }

        [Fact]
        public void Constructor_WithContextExtractorThatThrows_ShouldContinueWithoutParentContext()
        {
            // Arrange
            var messageHeaders = new Dictionary<string, object>
            {
                { "invalid-header", "bad-value" }
            };

            Func<IDictionary<string, object>, ActivityContext> contextExtractor = (headers) => 
                throw new InvalidOperationException("Context extraction failed");
            
            TelemetryManager.SetTraceContextExtractor(contextExtractor);

            // Act & Assert - Should not throw
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object, messageHeaders);
            
            // Should still create activity with default context
            _mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, It.IsAny<ActivityContext>()), Times.Once);
        }

        [Fact]
        public void SetTag_ShouldDelegateToActivity()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            telemetry.SetTag("custom.tag", "custom.value");

            // Assert
            _mockActivity.Verify(a => a.SetTag("custom.tag", "custom.value"), Times.Once);
        }

        [Fact]
        public void SetTag_WithNullActivity_ShouldNotThrow()
        {
            // Arrange
            _mockProvider.Setup(p => p.StartActivity(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>()))
                .Returns((IActivity)null);
            
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            Action act = () => telemetry.SetTag("custom.tag", "custom.value");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordSuccess_ShouldRecordMetricsAndSetStatus()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            telemetry.RecordSuccess(MessageStatus.MESSAGE_DONE);

            // Assert
            _mockMetrics.Verify(m => m.RecordTaskProcessed("test-queue", "worker-1"), Times.Once);
            _mockMetrics.Verify(m => m.RecordTaskDuration(It.IsAny<double>(), "test-queue", "worker-1"), Times.Once);
            _mockActivity.Verify(a => a.SetTag("message.status", "MESSAGE_DONE"), Times.Once);
            _mockActivity.Verify(a => a.SetStatus(ActivityStatus.Ok, null), Times.Once);
        }

        [Fact]
        public void RecordSuccess_WithMessageDoneWithReply_ShouldSetCorrectStatus()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            telemetry.RecordSuccess(MessageStatus.MESSAGE_DONE_WITH_REPLY);

            // Assert
            _mockMetrics.Verify(m => m.RecordTaskProcessed("test-queue", "worker-1"), Times.Once);
            _mockActivity.Verify(a => a.SetTag("message.status", "MESSAGE_DONE_WITH_REPLY"), Times.Once);
            _mockActivity.Verify(a => a.SetStatus(ActivityStatus.Ok, null), Times.Once);
        }

        [Fact]
        public void RecordSuccess_CalledMultipleTimes_ShouldOnlyRecordOnce()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            telemetry.RecordSuccess(MessageStatus.MESSAGE_DONE);
            telemetry.RecordSuccess(MessageStatus.MESSAGE_DONE);
            telemetry.RecordSuccess(MessageStatus.MESSAGE_DONE);

            // Assert
            _mockMetrics.Verify(m => m.RecordTaskProcessed(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void RecordSuccess_WithNullMetrics_ShouldNotThrow()
        {
            // Arrange
            _mockProvider.Setup(p => p.Metrics).Returns((IMetrics)null);
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            Action act = () => telemetry.RecordSuccess(MessageStatus.MESSAGE_DONE);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordRejection_ShouldRecordMetricsAndSetStatus()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            telemetry.RecordRejection(MessageStatus.IGNORE_MESSAGE);

            // Assert
            _mockMetrics.Verify(m => m.RecordTaskRejected("test-queue", "worker-1"), Times.Once);
            _mockMetrics.Verify(m => m.RecordTaskDuration(It.IsAny<double>(), "test-queue", "worker-1"), Times.Once);
            _mockActivity.Verify(a => a.SetTag("message.status", "IGNORE_MESSAGE"), Times.Once);
            _mockActivity.Verify(a => a.SetStatus(ActivityStatus.Ok, null));
        }

        [Fact]
        public void RecordRejection_CalledMultipleTimes_ShouldOnlyRecordOnce()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            telemetry.RecordRejection(MessageStatus.IGNORE_MESSAGE);
            telemetry.RecordRejection(MessageStatus.IGNORE_MESSAGE);

            // Assert
            _mockMetrics.Verify(m => m.RecordTaskRejected(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void RecordRejection_WithNullMetrics_ShouldNotThrow()
        {
            // Arrange
            _mockProvider.Setup(p => p.Metrics).Returns((IMetrics)null);
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            Action act = () => telemetry.RecordRejection(MessageStatus.IGNORE_MESSAGE);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordFailure_ShouldRecordMetricsAndException()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);
            var exception = new InvalidOperationException("Test exception");

            // Act
            telemetry.RecordFailure(exception);

            // Assert
            _mockMetrics.Verify(m => m.RecordTaskFailed("test-queue", "worker-1", "InvalidOperationException"), Times.Once);
            _mockMetrics.Verify(m => m.RecordTaskDuration(It.IsAny<double>(), "test-queue", "worker-1"), Times.Once);
            _mockActivity.Verify(a => a.SetStatus(ActivityStatus.Error, "Test exception"), Times.Once);
            _mockActivity.Verify(a => a.RecordException(exception), Times.Once);
        }

        [Fact]
        public void RecordFailure_ShouldLogWarning()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);
            var exception = new InvalidOperationException("Test exception");

            // Act
            telemetry.RecordFailure(exception);

            // Assert
            _mockLogger.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, t) => true), exception, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public void RecordFailure_CalledMultipleTimes_ShouldOnlyRecordOnce()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);
            var exception = new InvalidOperationException("Test exception");

            // Act
            telemetry.RecordFailure(exception);
            telemetry.RecordFailure(exception);

            // Assert
            _mockMetrics.Verify(m => m.RecordTaskFailed(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void RecordFailure_WithNullMetrics_ShouldNotThrow()
        {
            // Arrange
            _mockProvider.Setup(p => p.Metrics).Returns((IMetrics)null);
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);
            var exception = new InvalidOperationException("Test exception");

            // Act
            Action act = () => telemetry.RecordFailure(exception);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordFailure_WithNullActivity_ShouldNotThrow()
        {
            // Arrange
            _mockProvider.Setup(p => p.StartActivity(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>()))
                .Returns((IActivity)null);
            
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);
            var exception = new InvalidOperationException("Test exception");

            // Act
            Action act = () => telemetry.RecordFailure(exception);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_ShouldDecrementProcessingTasksAndDisposeActivity()
        {
            // Arrange
            var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            telemetry.Dispose();

            // Assert
            _mockMetrics.Verify(m => m.DecrementProcessingTasks(), Times.Once);
            _mockActivity.Verify(a => a.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldOnlyExecuteOnce()
        {
            // Arrange
            var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            telemetry.Dispose();
            telemetry.Dispose();
            telemetry.Dispose();

            // Assert
            _mockMetrics.Verify(m => m.DecrementProcessingTasks(), Times.Once);
            _mockActivity.Verify(a => a.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_WithNullMetrics_ShouldNotThrow()
        {
            // Arrange
            _mockProvider.Setup(p => p.Metrics).Returns((IMetrics)null);
            var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            Action act = () => telemetry.Dispose();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_WithNullActivity_ShouldNotThrow()
        {
            // Arrange
            _mockProvider.Setup(p => p.StartActivity(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>()))
                .Returns((IActivity)null);
            
            var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            Action act = () => telemetry.Dispose();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordSuccess_AfterRecordFailure_ShouldNotRecord()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);
            var exception = new InvalidOperationException("Test exception");

            // Act
            telemetry.RecordFailure(exception);
            telemetry.RecordSuccess(MessageStatus.MESSAGE_DONE);

            // Assert
            _mockMetrics.Verify(m => m.RecordTaskFailed(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _mockMetrics.Verify(m => m.RecordTaskProcessed(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void RecordRejection_AfterRecordSuccess_ShouldNotRecord()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);

            // Act
            telemetry.RecordSuccess(MessageStatus.MESSAGE_DONE);
            telemetry.RecordRejection(MessageStatus.IGNORE_MESSAGE);

            // Assert
            _mockMetrics.Verify(m => m.RecordTaskProcessed(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _mockMetrics.Verify(m => m.RecordTaskRejected(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void RecordTaskDuration_ShouldBeMeasuredAccurately()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", _mockLogger.Object);
            
            // Act - Simulate some processing time
            System.Threading.Thread.Sleep(100);
            telemetry.RecordSuccess(MessageStatus.MESSAGE_DONE);

            // Assert - Duration should be at least 100ms (allowing for some variance)
            _mockMetrics.Verify(m => m.RecordTaskDuration(It.Is<double>(d => d >= 90), "test-queue", "worker-1"), Times.Once);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldNotThrow()
        {
            // Arrange & Act
            Action act = () =>
            {
                using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", null);
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordFailure_WithNullLogger_ShouldNotThrow()
        {
            // Arrange
            using var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", null);
            var exception = new InvalidOperationException("Test exception");

            // Act
            Action act = () => telemetry.RecordFailure(exception);

            // Assert
            act.Should().NotThrow();
        }
    }
}
