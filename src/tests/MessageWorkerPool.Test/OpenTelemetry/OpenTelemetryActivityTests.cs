using FluentAssertions;
using MessageWorkerPool.OpenTelemetry;
using MessageWorkerPool.Telemetry.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace MessageWorkerPool.Test.OpenTelemetry
{
    public class OpenTelemetryActivityTests : IDisposable
    {
        private readonly ActivitySource _activitySource;
        private readonly ActivityListener _listener;
        private readonly List<Activity> _activities;

        public OpenTelemetryActivityTests()
        {
            _activitySource = new ActivitySource("TestSource", "1.0.0");
            _activities = new List<Activity>();
            
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "TestSource",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => _activities.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose()
        {
            _activitySource?.Dispose();
            _listener?.Dispose();
        }

        [Fact]
        public void Constructor_WithNullActivity_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new OpenTelemetryActivity(null);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("activity");
        }

        [Fact]
        public void Constructor_WithValidActivity_ShouldCreateInstance()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");

            // Act
            var otelActivity = new OpenTelemetryActivity(activity);

            // Assert
            otelActivity.Should().NotBeNull();
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetTag_ShouldSetTagOnUnderlyingActivity()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            otelActivity.SetTag("test.key", "test.value");

            // Assert
            activity.GetTagItem("test.key").Should().Be("test.value");
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetTag_WithNullKey_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            Action act = () => otelActivity.SetTag(null, "value");

            // Assert
            act.Should().NotThrow();
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetTag_WithEmptyKey_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            Action act = () => otelActivity.SetTag(string.Empty, "value");

            // Assert
            act.Should().NotThrow();
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetTag_WithNullValue_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            Action act = () => otelActivity.SetTag("key", null);

            // Assert
            act.Should().NotThrow();
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetTag_WithNumericValue_ShouldConvertToString()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            otelActivity.SetTag("number", 42);

            // Assert
            activity.GetTagItem("number").Should().Be("42");
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetTags_WithMultipleTags_ShouldSetAllTags()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);
            var tags = new Dictionary<string, object>
            {
                { "tag1", "value1" },
                { "tag2", 42 },
                { "tag3", true }
            };

            // Act
            otelActivity.SetTags(tags);

            // Assert
            activity.GetTagItem("tag1").Should().Be("value1");
            activity.GetTagItem("tag2").Should().Be("42");
            activity.GetTagItem("tag3").Should().Be("True");
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetTags_WithNullDictionary_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            Action act = () => otelActivity.SetTags(null);

            // Assert
            act.Should().NotThrow();
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetTags_WithEmptyDictionary_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            Action act = () => otelActivity.SetTags(new Dictionary<string, object>());

            // Assert
            act.Should().NotThrow();
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetStatus_WithOkStatus_ShouldSetActivityStatus()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            otelActivity.SetStatus(ActivityStatus.Ok);

            // Assert
            activity.Status.Should().Be(ActivityStatusCode.Ok);
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetStatus_WithErrorStatus_ShouldSetActivityStatus()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            otelActivity.SetStatus(ActivityStatus.Error, "Test error");

            // Assert
            activity.Status.Should().Be(ActivityStatusCode.Error);
            activity.StatusDescription.Should().Be("Test error");
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void SetStatus_WithNullDescription_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            Action act = () => otelActivity.SetStatus(ActivityStatus.Ok, null);

            // Assert
            act.Should().NotThrow();
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void RecordException_ShouldSetExceptionTags()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);
            
            // Create an exception with a stack trace by actually throwing it
            Exception exception = null;
            try
            {
                throw new InvalidOperationException("Test exception");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Act
            otelActivity.RecordException(exception);

            // Assert
            activity.GetTagItem("exception.type").Should().Be("System.InvalidOperationException");
            activity.GetTagItem("exception.message").Should().Be("Test exception");
            // Stack trace should exist since we actually threw the exception
            var stackTrace = activity.GetTagItem("exception.stacktrace") as string;
            stackTrace.Should().NotBeNullOrEmpty();
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void RecordException_WithNullException_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            Action act = () => otelActivity.RecordException(null);

            // Assert
            act.Should().NotThrow();
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void RecordException_WithExceptionWithStackTrace_ShouldCaptureStackTrace()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);
            
            Exception exception = null;
            try
            {
                throw new InvalidOperationException("Test with stack trace");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Act
            otelActivity.RecordException(exception);

            // Assert
            var stackTrace = activity.GetTagItem("exception.stacktrace") as string;
            stackTrace.Should().NotBeNullOrEmpty();
            stackTrace.Should().Contain("RecordException_WithExceptionWithStackTrace_ShouldCaptureStackTrace");
            
            // Cleanup
            otelActivity.Dispose();
        }

        [Fact]
        public void Dispose_ShouldDisposeUnderlyingActivity()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            otelActivity.Dispose();

            // Assert - Activity should be stopped/disposed
            // We can't directly verify disposal, but we can check operations after disposal don't throw
            Action act = () => otelActivity.SetTag("test", "value");
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);

            // Act
            Action act = () =>
            {
                otelActivity.Dispose();
                otelActivity.Dispose();
                otelActivity.Dispose();
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetTag_AfterDispose_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);
            otelActivity.Dispose();

            // Act
            Action act = () => otelActivity.SetTag("test", "value");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetStatus_AfterDispose_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);
            otelActivity.Dispose();

            // Act
            Action act = () => otelActivity.SetStatus(ActivityStatus.Ok);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RecordException_AfterDispose_ShouldNotThrow()
        {
            // Arrange
            var activity = _activitySource.StartActivity("TestOperation");
            var otelActivity = new OpenTelemetryActivity(activity);
            otelActivity.Dispose();
            var exception = new InvalidOperationException("Test");

            // Act
            Action act = () => otelActivity.RecordException(exception);

            // Assert
            act.Should().NotThrow();
        }
    }
}
