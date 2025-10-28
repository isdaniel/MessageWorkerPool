using FluentAssertions;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.Telemetry.Abstractions;
using MessageWorkerPool.Utilities;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace MessageWorkerPool.Test.Telemetry
{
    [Collection("TelemetryTests")]
    public class TelemetryManagerTests : IDisposable
    {
        public void Dispose()
        {
            // Clean up - reset to NoOp provider after each test
            TelemetryManager.SetProvider(NoOpTelemetryProvider.Instance);
            TelemetryManager.SetTraceContextExtractor(null);
        }

        [Fact]
        public void SetProvider_WithValidProvider_ShouldSetProvider()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();

            // Act
            TelemetryManager.SetProvider(mockProvider.Object);

            // Assert
            TelemetryManager.Provider.Should().Be(mockProvider.Object);
        }

        [Fact]
        public void SetProvider_WithNull_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => TelemetryManager.SetProvider(null);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("provider");
        }

        [Fact]
        public void Provider_ByDefault_ShouldReturnNoOpProvider()
        {
            // Arrange - Reset to default
            TelemetryManager.SetProvider(NoOpTelemetryProvider.Instance);

            // Act
            var provider = TelemetryManager.Provider;

            // Assert
            provider.Should().BeOfType<NoOpTelemetryProvider>();
        }

        [Fact]
        public void SetTraceContextExtractor_WithValidExtractor_ShouldSetExtractor()
        {
            // Arrange
            Func<IDictionary<string, object>, ActivityContext> extractor = (headers) => default;

            // Act & Assert - Should not throw
            Action act = () => TelemetryManager.SetTraceContextExtractor(extractor);
            act.Should().NotThrow();
        }

        [Fact]
        public void SetTraceContextExtractor_WithNull_ShouldNotThrow()
        {
            // Act & Assert
            Action act = () => TelemetryManager.SetTraceContextExtractor(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void StartWorkerInitActivity_ShouldCreateActivityWithCorrectTags()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.Init", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            TelemetryManager.SetProvider(mockProvider.Object);

            // Act
            var activity = TelemetryManager.StartWorkerInitActivity("worker-1", "test-queue");

            // Assert
            activity.Should().Be(mockActivity.Object);
            mockProvider.Verify(p => p.StartActivity("Worker.Init", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>()), Times.Once);
            mockActivity.Verify(a => a.SetTag("worker.id", "worker-1"), Times.Once);
            mockActivity.Verify(a => a.SetTag("queue.name", "test-queue"), Times.Once);
        }

        [Fact]
        public void StartTaskProcessingActivity_ShouldCreateActivityWithCorrectTags()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            TelemetryManager.SetProvider(mockProvider.Object);

            // Act
            var activity = TelemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123");

            // Assert
            activity.Should().Be(mockActivity.Object);
            mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, default(ActivityContext)), Times.Once);
            mockActivity.Verify(a => a.SetTag("worker.id", "worker-1"), Times.Once);
            mockActivity.Verify(a => a.SetTag("queue.name", "test-queue"), Times.Once);
            mockActivity.Verify(a => a.SetTag("correlation.id", "corr-123"), Times.Once);
            mockActivity.Verify(a => a.SetTag("messaging.system", "rabbitmq"), Times.Once);
            mockActivity.Verify(a => a.SetTag("messaging.destination", "test-queue"), Times.Once);
            mockActivity.Verify(a => a.SetTag("messaging.operation", "process"), Times.Once);
        }

        [Fact]
        public void StartTaskProcessingActivity_WithMessageHeaders_ShouldCreateActivityWithConsumerKind()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            TelemetryManager.SetProvider(mockProvider.Object);
            
            var messageHeaders = new Dictionary<string, object>
            {
                { "custom-header", "custom-value" }
            };

            // Act
            var activity = TelemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert
            activity.Should().Be(mockActivity.Object);
            mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, default(ActivityContext)), Times.Once);
        }

        [Fact]
        public void StartTaskProcessingActivity_WithMessageHeadersAndExtractor_ShouldExtractParentContext()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            TelemetryManager.SetProvider(mockProvider.Object);
            
            var expectedContext = new ActivityContext(
                ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()),
                ActivitySpanId.CreateFromString("b7ad6b7169203331".AsSpan()),
                ActivityTraceFlags.Recorded);

            var messageHeaders = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };

            Func<IDictionary<string, object>, ActivityContext> extractor = (headers) => expectedContext;
            TelemetryManager.SetTraceContextExtractor(extractor);

            // Act
            var activity = TelemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert
            mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, expectedContext), Times.Once);
        }

        [Fact]
        public void StartTaskProcessingActivity_WithExtractorThatThrows_ShouldUseDefaultContext()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            TelemetryManager.SetProvider(mockProvider.Object);
            
            var messageHeaders = new Dictionary<string, object>
            {
                { "invalid-header", "bad-value" }
            };

            Func<IDictionary<string, object>, ActivityContext> extractor = (headers) => 
                throw new InvalidOperationException("Extraction failed");
            
            TelemetryManager.SetTraceContextExtractor(extractor);

            // Act
            var activity = TelemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert - Should use default context instead of throwing
            mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, default(ActivityContext)), Times.Once);
        }

        [Fact]
        public void StartTaskProcessingActivity_WithNullMessageHeaders_ShouldUseDefaultContext()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            TelemetryManager.SetProvider(mockProvider.Object);

            Func<IDictionary<string, object>, ActivityContext> extractor = (headers) => 
                new ActivityContext(
                    ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()),
                    ActivitySpanId.CreateFromString("b7ad6b7169203331".AsSpan()),
                    ActivityTraceFlags.Recorded);
            
            TelemetryManager.SetTraceContextExtractor(extractor);

            // Act
            var activity = TelemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", null);

            // Assert - Extractor should not be called when messageHeaders is null
            mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, default(ActivityContext)), Times.Once);
        }

        [Fact]
        public void StartTaskProcessingActivity_WithoutExtractor_ShouldUseDefaultContext()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            TelemetryManager.SetProvider(mockProvider.Object);

            var messageHeaders = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };

            // Don't set extractor

            // Act
            var activity = TelemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert - Should use default context when no extractor is set
            mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, default(ActivityContext)), Times.Once);
        }

        [Fact]
        public void StartPoolInitActivity_ShouldCreateActivityWithCorrectTags()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("WorkerPool.Init", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            TelemetryManager.SetProvider(mockProvider.Object);

            // Act
            var activity = TelemetryManager.StartPoolInitActivity(5, "test-queue");

            // Assert
            activity.Should().Be(mockActivity.Object);
            mockProvider.Verify(p => p.StartActivity("WorkerPool.Init", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>()), Times.Once);
            mockActivity.Verify(a => a.SetTag("workerpool.worker_count", 5), Times.Once);
            mockActivity.Verify(a => a.SetTag("queue.name", "test-queue"), Times.Once);
        }

        [Fact]
        public void StartShutdownActivity_ShouldCreateActivityWithCorrectTags()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.Shutdown", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            TelemetryManager.SetProvider(mockProvider.Object);

            // Act
            var activity = TelemetryManager.StartShutdownActivity("worker-1");

            // Assert
            activity.Should().Be(mockActivity.Object);
            mockProvider.Verify(p => p.StartActivity("Worker.Shutdown", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>()), Times.Once);
            mockActivity.Verify(a => a.SetTag("worker.id", "worker-1"), Times.Once);
        }

        [Fact]
        public void RecordException_WithValidActivityAndException_ShouldRecordException()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var exception = new InvalidOperationException("Test exception");

            // Act
            TelemetryManager.RecordException(mockActivity.Object, exception);

            // Assert
            mockActivity.Verify(a => a.SetStatus(ActivityStatus.Error, "Test exception"), Times.Once);
            mockActivity.Verify(a => a.RecordException(exception), Times.Once);
        }

        [Fact]
        public void RecordException_WithNullActivity_ShouldNotThrow()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            Action act = () => TelemetryManager.RecordException(null, exception);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordException_WithNullException_ShouldNotThrow()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();

            // Act
            Action act = () => TelemetryManager.RecordException(mockActivity.Object, null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordException_WithBothNull_ShouldNotThrow()
        {
            // Act
            Action act = () => TelemetryManager.RecordException(null, null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetTaskStatus_WithMessageDone_ShouldSetOkStatus()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();

            // Act
            TelemetryManager.SetTaskStatus(mockActivity.Object, MessageStatus.MESSAGE_DONE);

            // Assert
            mockActivity.Verify(a => a.SetTag("message.status", "MESSAGE_DONE"), Times.Once);
            mockActivity.Verify(a => a.SetStatus(ActivityStatus.Ok, null), Times.Once);
        }

        [Fact]
        public void SetTaskStatus_WithMessageDoneWithReply_ShouldSetOkStatus()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();

            // Act
            TelemetryManager.SetTaskStatus(mockActivity.Object, MessageStatus.MESSAGE_DONE_WITH_REPLY);

            // Assert
            mockActivity.Verify(a => a.SetTag("message.status", "MESSAGE_DONE_WITH_REPLY"), Times.Once);
            mockActivity.Verify(a => a.SetStatus(ActivityStatus.Ok, null), Times.Once);
        }

        [Fact]
        public void SetTaskStatus_WithIgnoreMessage_ShouldSetErrorStatus()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();

            // Act
            TelemetryManager.SetTaskStatus(mockActivity.Object, MessageStatus.IGNORE_MESSAGE);

            // Assert
            mockActivity.Verify(a => a.SetTag("message.status", "IGNORE_MESSAGE"), Times.Once);
            mockActivity.Verify(a => a.SetStatus(ActivityStatus.Error, "Message ignored"), Times.Once);
        }

        [Fact]
        public void SetTaskStatus_WithNullActivity_ShouldNotThrow()
        {
            // Act
            Action act = () => TelemetryManager.SetTaskStatus(null, MessageStatus.MESSAGE_DONE);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Metrics_ShouldReturnProviderMetrics()
        {
            // Arrange - Create fresh provider for this test
            var mockMetrics = new Mock<IMetrics>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.Metrics).Returns(mockMetrics.Object);
            
            // Set provider and immediately get metrics
            TelemetryManager.SetProvider(mockProvider.Object);

            // Act
            var metrics = TelemetryManager.Metrics;

            // Assert
            metrics.Should().Be(mockMetrics.Object);
            
            // Cleanup - restore original provider
            TelemetryManager.SetProvider(NoOpTelemetryProvider.Instance);
        }

        [Fact]
        public void StartWorkerInitActivity_WithNullProvider_ShouldReturnNull()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.Init", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns((IActivity)null);
            TelemetryManager.SetProvider(mockProvider.Object);

            // Act
            var activity = TelemetryManager.StartWorkerInitActivity("worker-1", "test-queue");

            // Assert
            activity.Should().BeNull();
        }

        [Fact]
        public void StartTaskProcessingActivity_WithNoOpProvider_ShouldReturnNoOpActivity()
        {
            // Arrange
            TelemetryManager.SetProvider(NoOpTelemetryProvider.Instance);

            // Act
            var activity = TelemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123");

            // Assert
            activity.Should().NotBeNull();
            activity.Should().BeOfType<NoOpActivity>();
        }

        [Fact]
        public void StartPoolInitActivity_WithNullActivityFromProvider_ShouldHandleGracefully()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("WorkerPool.Init", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns((IActivity)null);
            TelemetryManager.SetProvider(mockProvider.Object);

            // Act
            var activity = TelemetryManager.StartPoolInitActivity(5, "test-queue");

            // Assert
            activity.Should().BeNull();
        }

        [Fact]
        public void StartShutdownActivity_WithNullActivityFromProvider_ShouldHandleGracefully()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.Shutdown", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns((IActivity)null);
            TelemetryManager.SetProvider(mockProvider.Object);

            // Act
            var activity = TelemetryManager.StartShutdownActivity("worker-1");

            // Assert
            activity.Should().BeNull();
        }
    }
}
