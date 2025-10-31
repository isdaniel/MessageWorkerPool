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
    public class TelemetryManagerTests
    {
        [Fact]
        public void Constructor_WithValidProvider_ShouldSetProvider()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();

            // Act
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Assert
            telemetryManager.Provider.Should().Be(mockProvider.Object);
        }

        [Fact]
        public void Constructor_WithNull_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new TelemetryManager(null);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("provider");
        }

        [Fact]
        public void Constructor_WithNoOpProvider_ShouldWork()
        {
            // Act
            var telemetryManager = new TelemetryManager(NoOpTelemetryProvider.Instance);

            // Assert
            telemetryManager.Provider.Should().BeOfType<NoOpTelemetryProvider>();
        }

        [Fact]
        public void Constructor_WithCustomExtractor_ShouldUseCustomExtractor()
        {
            // Arrange
            Func<IDictionary<string, object>, ActivityContext> extractor = (headers) => default;
            var mockProvider = new Mock<ITelemetryProvider>();

            // Act & Assert - Should not throw
            Action act = () => new TelemetryManager(mockProvider.Object, extractor);
            act.Should().NotThrow();
        }

        [Fact]
        public void StartWorkerInitActivity_ShouldCreateActivityWithCorrectTags()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.Init", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var activity = telemetryManager.StartWorkerInitActivity("worker-1", "test-queue");

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
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123");

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
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            var messageHeaders = new Dictionary<string, object>
            {
                { "custom-header", "custom-value" }
            };

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

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

            var expectedContext = new ActivityContext(
                ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()),
                ActivitySpanId.CreateFromString("b7ad6b7169203331".AsSpan()),
                ActivityTraceFlags.Recorded);

            var messageHeaders = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };

            Func<IDictionary<string, object>, ActivityContext> extractor = (headers) => expectedContext;
            var telemetryManager = new TelemetryManager(mockProvider.Object, extractor);

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

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

            var messageHeaders = new Dictionary<string, object>
            {
                { "invalid-header", "bad-value" }
            };

            Func<IDictionary<string, object>, ActivityContext> extractor = (headers) =>
                throw new InvalidOperationException("Extraction failed");

            var telemetryManager = new TelemetryManager(mockProvider.Object, extractor);

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

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

            Func<IDictionary<string, object>, ActivityContext> extractor = (headers) =>
                new ActivityContext(
                    ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()),
                    ActivitySpanId.CreateFromString("b7ad6b7169203331".AsSpan()),
                    ActivityTraceFlags.Recorded);

            var telemetryManager = new TelemetryManager(mockProvider.Object, extractor);

            // Act - Explicitly cast null to the correct type to avoid ambiguity
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", (IDictionary<string, object>)null);

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

            var messageHeaders = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };

            // Create telemetryManager without custom extractor - should use default TraceContextPropagation.ExtractTraceContext
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert - Should extract context from traceparent header using default extractor
            // The extracted context should have TraceId = "0af7651916cd43dd8448eb211c80319c"
            mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer,
                It.Is<ActivityContext>(ctx => ctx.TraceId.ToString() == "0af7651916cd43dd8448eb211c80319c")), Times.Once);
        }

        [Fact]
        public void StartPoolInitActivity_ShouldCreateActivityWithCorrectTags()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("WorkerPool.Init", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var activity = telemetryManager.StartPoolInitActivity(5, "test-queue");

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
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var activity = telemetryManager.StartShutdownActivity("worker-1");

            // Assert
            activity.Should().Be(mockActivity.Object);
            mockProvider.Verify(p => p.StartActivity("Worker.Shutdown", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>()), Times.Once);
            mockActivity.Verify(a => a.SetTag("worker.id", "worker-1"), Times.Once);
        }

        [Fact]
        public void RecordException_WithValidActivityAndException_ShouldRecordException()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            var telemetryManager = new TelemetryManager(mockProvider.Object);
            var mockActivity = new Mock<IActivity>();
            var exception = new InvalidOperationException("Test exception");

            // Act
            telemetryManager.RecordException(mockActivity.Object, exception);

            // Assert
            mockActivity.Verify(a => a.SetStatus(ActivityStatus.Error, "Test exception"), Times.Once);
            mockActivity.Verify(a => a.RecordException(exception), Times.Once);
        }

        [Fact]
        public void RecordException_WithNullActivity_ShouldNotThrow()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            var telemetryManager = new TelemetryManager(mockProvider.Object);
            var exception = new InvalidOperationException("Test exception");

            // Act
            Action act = () => telemetryManager.RecordException(null, exception);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordException_WithNullException_ShouldNotThrow()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            var telemetryManager = new TelemetryManager(mockProvider.Object);
            var mockActivity = new Mock<IActivity>();

            // Act
            Action act = () => telemetryManager.RecordException(mockActivity.Object, null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordException_WithBothNull_ShouldNotThrow()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            Action act = () => telemetryManager.RecordException(null, null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetTaskStatus_WithMessageDone_ShouldSetOkStatus()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            var telemetryManager = new TelemetryManager(mockProvider.Object);
            var mockActivity = new Mock<IActivity>();

            // Act
            telemetryManager.SetTaskStatus(mockActivity.Object, MessageStatus.MESSAGE_DONE);

            // Assert
            mockActivity.Verify(a => a.SetTag("message.status", "MESSAGE_DONE"), Times.Once);
            mockActivity.Verify(a => a.SetStatus(ActivityStatus.Ok, null), Times.Once);
        }

        [Fact]
        public void SetTaskStatus_WithMessageDoneWithReply_ShouldSetOkStatus()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            var telemetryManager = new TelemetryManager(mockProvider.Object);
            var mockActivity = new Mock<IActivity>();

            // Act
            telemetryManager.SetTaskStatus(mockActivity.Object, MessageStatus.MESSAGE_DONE_WITH_REPLY);

            // Assert
            mockActivity.Verify(a => a.SetTag("message.status", "MESSAGE_DONE_WITH_REPLY"), Times.Once);
            mockActivity.Verify(a => a.SetStatus(ActivityStatus.Ok, null), Times.Once);
        }

        [Fact]
        public void SetTaskStatus_WithIgnoreMessage_ShouldSetErrorStatus()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            var telemetryManager = new TelemetryManager(mockProvider.Object);
            var mockActivity = new Mock<IActivity>();

            // Act
            telemetryManager.SetTaskStatus(mockActivity.Object, MessageStatus.IGNORE_MESSAGE);

            // Assert
            mockActivity.Verify(a => a.SetTag("message.status", "IGNORE_MESSAGE"), Times.Once);
            mockActivity.Verify(a => a.SetStatus(ActivityStatus.Ok, null), Times.Once);
        }

        [Fact]
        public void SetTaskStatus_WithNullActivity_ShouldNotThrow()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            Action act = () => telemetryManager.SetTaskStatus(null, MessageStatus.MESSAGE_DONE);

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
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var metrics = telemetryManager.Metrics;

            // Assert
            metrics.Should().Be(mockMetrics.Object);
        }

        [Fact]
        public void StartWorkerInitActivity_WithNullProvider_ShouldReturnNull()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.Init", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns((IActivity)null);
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var activity = telemetryManager.StartWorkerInitActivity("worker-1", "test-queue");

            // Assert
            activity.Should().BeNull();
        }

        [Fact]
        public void StartTaskProcessingActivity_WithNoOpProvider_ShouldReturnNoOpActivity()
        {
            // Arrange
            var telemetryManager = new TelemetryManager(NoOpTelemetryProvider.Instance);

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123");

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
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var activity = telemetryManager.StartPoolInitActivity(5, "test-queue");

            // Assert
            activity.Should().BeNull();
        }

        [Fact]
        public void StartShutdownActivity_WithNullActivityFromProvider_ShouldHandleGracefully()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.Shutdown", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns((IActivity)null);
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var activity = telemetryManager.StartShutdownActivity("worker-1");

            // Assert
            activity.Should().BeNull();
        }

        [Fact]
        public void StartTaskProcessingActivity_WithNullActivity_ShouldHandleGracefully()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns((IActivity)null);
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123");

            // Assert
            activity.Should().BeNull();
        }

        #region String Dictionary Overload Tests

        [Fact]
        public void StartTaskProcessingActivity_StringDict_ShouldCreateActivityWithCorrectTags()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            var messageHeaders = new Dictionary<string, string>
            {
                { "custom-header", "custom-value" }
            };

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

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
        public void StartTaskProcessingActivity_StringDict_WithValidTraceContext_ShouldExtractParentContext()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();

            ActivityContext capturedContext = default;
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, It.IsAny<ActivityContext>()))
                .Callback<string, IDictionary<string, object>, ActivityKind, ActivityContext>((name, tags, kind, context) => capturedContext = context)
                .Returns(mockActivity.Object);

            var telemetryManager = new TelemetryManager(mockProvider.Object);

            var messageHeaders = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert
            activity.Should().Be(mockActivity.Object);
            capturedContext.Should().NotBe(default(ActivityContext));
            capturedContext.TraceId.Should().Be(ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()));
            capturedContext.SpanId.Should().Be(ActivitySpanId.CreateFromString("b7ad6b7169203331".AsSpan()));
            capturedContext.TraceFlags.Should().Be(ActivityTraceFlags.Recorded);
        }

        [Fact]
        public void StartTaskProcessingActivity_StringDict_WithTraceContextAndTraceState_ShouldExtractBoth()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();

            ActivityContext capturedContext = default;
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, It.IsAny<ActivityContext>()))
                .Callback<string, IDictionary<string, object>, ActivityKind, ActivityContext>((name, tags, kind, context) => capturedContext = context)
                .Returns(mockActivity.Object);

            var telemetryManager = new TelemetryManager(mockProvider.Object);

            var messageHeaders = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "tracestate", "vendor=value" }
            };

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert
            activity.Should().Be(mockActivity.Object);
            capturedContext.Should().NotBe(default(ActivityContext));
            capturedContext.TraceState.Should().Be("vendor=value");
        }

        [Fact]
        public void StartTaskProcessingActivity_StringDict_WithNullHeaders_ShouldUseDefaultContext()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", (IDictionary<string, string>)null);

            // Assert
            activity.Should().Be(mockActivity.Object);
            mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, default(ActivityContext)), Times.Once);
        }

        [Fact]
        public void StartTaskProcessingActivity_StringDict_WithEmptyHeaders_ShouldUseDefaultContext()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            var messageHeaders = new Dictionary<string, string>();

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert
            activity.Should().Be(mockActivity.Object);
            mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, default(ActivityContext)), Times.Once);
        }

        [Fact]
        public void StartTaskProcessingActivity_StringDict_WithInvalidTraceParent_ShouldUseDefaultContext()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns(mockActivity.Object);
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            var messageHeaders = new Dictionary<string, string>
            {
                { "traceparent", "invalid-traceparent-format" }
            };

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert
            activity.Should().Be(mockActivity.Object);
            mockProvider.Verify(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, default(ActivityContext)), Times.Once);
        }

        [Fact]
        public void StartTaskProcessingActivity_StringDict_WithMultipleCustomHeaders_ShouldNotAffectTraceContext()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();

            ActivityContext capturedContext = default;
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, It.IsAny<ActivityContext>()))
                .Callback<string, IDictionary<string, object>, ActivityKind, ActivityContext>((name, tags, kind, context) => capturedContext = context)
                .Returns(mockActivity.Object);

            var telemetryManager = new TelemetryManager(mockProvider.Object);

            var messageHeaders = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "custom-header-1", "value1" },
                { "custom-header-2", "value2" },
                { "custom-header-3", "value3" }
            };

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert
            activity.Should().Be(mockActivity.Object);
            capturedContext.Should().NotBe(default(ActivityContext));
            capturedContext.TraceId.Should().Be(ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()));
        }

        [Fact]
        public void StartTaskProcessingActivity_StringDict_WithNullActivityFromProvider_ShouldHandleGracefully()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", It.IsAny<IDictionary<string, object>>(), It.IsAny<ActivityKind>(), It.IsAny<ActivityContext>())).Returns((IActivity)null);
            var telemetryManager = new TelemetryManager(mockProvider.Object);

            var messageHeaders = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert
            activity.Should().BeNull();
        }

        [Fact]
        public void StartTaskProcessingActivity_StringDict_WithCustomExtractor_ShouldUseCustomExtractor()
        {
            // Arrange
            var mockActivity = new Mock<IActivity>();
            var mockProvider = new Mock<ITelemetryProvider>();

            var customContext = new ActivityContext(
                ActivityTraceId.CreateFromString("1234567890abcdef1234567890abcdef".AsSpan()),
                ActivitySpanId.CreateFromString("abcdef1234567890".AsSpan()),
                ActivityTraceFlags.Recorded);

            ActivityContext capturedContext = default;
            mockProvider.Setup(p => p.StartActivity("Worker.ProcessTask", null, ActivityKind.Consumer, It.IsAny<ActivityContext>()))
                .Callback<string, IDictionary<string, object>, ActivityKind, ActivityContext>((name, tags, kind, context) => capturedContext = context)
                .Returns(mockActivity.Object);

            // Set custom extractor
            Func<IDictionary<string, object>, ActivityContext> customExtractor = (headers) => customContext;
            var telemetryManager = new TelemetryManager(mockProvider.Object, customExtractor);

            var messageHeaders = new Dictionary<string, string>
            {
                { "custom-trace-header", "custom-value" }
            };

            // Act
            var activity = telemetryManager.StartTaskProcessingActivity("worker-1", "test-queue", "corr-123", messageHeaders);

            // Assert
            activity.Should().Be(mockActivity.Object);
            capturedContext.Should().Be(customContext);
            capturedContext.TraceId.Should().Be(ActivityTraceId.CreateFromString("1234567890abcdef1234567890abcdef".AsSpan()));
        }

        #endregion

        #region SetTaskStatus Additional Tests

        [Fact]
        public void SetTaskStatus_WithUnknownError_ShouldSetErrorStatus()
        {
            // Arrange
            var mockProvider = new Mock<ITelemetryProvider>();
            var telemetryManager = new TelemetryManager(mockProvider.Object);
            var mockActivity = new Mock<IActivity>();

            // Act
            telemetryManager.SetTaskStatus(mockActivity.Object, MessageStatus.UNKNOWN_ERROR);

            // Assert
            mockActivity.Verify(a => a.SetTag("message.status", "UNKNOWN_ERROR"), Times.Once);
            mockActivity.Verify(a => a.SetStatus(ActivityStatus.Error, "UNKNOWN_ERROR"), Times.Once);
        }

        #endregion
    }
}



