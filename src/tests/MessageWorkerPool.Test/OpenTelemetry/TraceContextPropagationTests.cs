using FluentAssertions;
using MessageWorkerPool.OpenTelemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace MessageWorkerPool.Test.OpenTelemetry
{
    public class TraceContextPropagationTests : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly ActivitySource _activitySource;

        public TraceContextPropagationTests()
        {
            _activitySource = new ActivitySource("test-source");
            
            // Create and start an ActivityListener to enable activity creation
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "test-source",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose()
        {
            _listener?.Dispose();
            _activitySource?.Dispose();
        }

        [Fact]
        public void ExtractTraceContext_WithValidTraceParent_ShouldReturnActivityContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().NotBe(default(ActivityContext));
            context.TraceId.Should().Be(ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()));
            context.SpanId.Should().Be(ActivitySpanId.CreateFromString("b7ad6b7169203331".AsSpan()));
            context.TraceFlags.Should().Be(ActivityTraceFlags.Recorded);
        }

        [Fact]
        public void ExtractTraceContext_WithTraceParentAndTraceState_ShouldReturnContextWithTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "tracestate", "congo=t61rcWkgMzE" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().NotBe(default(ActivityContext));
            context.TraceState.Should().Be("congo=t61rcWkgMzE");
        }

        [Fact]
        public void ExtractTraceContext_WithNullHeaders_ShouldReturnDefaultContext()
        {
            // Act
            var context = TraceContextPropagation.ExtractTraceContext(null);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithEmptyHeaders_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>();

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithMissingTraceParent_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "other-header", "value" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithNullTraceParent_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", null }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithEmptyTraceParent_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithWhitespaceTraceParent_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "   " }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithInvalidFormat_TooFewParts_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithInvalidFormat_TooManyParts_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01-extra" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithInvalidTraceId_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-invalid-trace-id-b7ad6b7169203331-01" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithInvalidSpanId_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-invalid-span-01" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithInvalidTraceFlags_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-zz" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_WithByteArrayTraceParent_ShouldParseCorrectly()
        {
            // Arrange
            var traceParentBytes = Encoding.UTF8.GetBytes("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");
            var headers = new Dictionary<string, object>
            {
                { "traceparent", traceParentBytes }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().NotBe(default(ActivityContext));
            context.TraceId.Should().Be(ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()));
            context.SpanId.Should().Be(ActivitySpanId.CreateFromString("b7ad6b7169203331".AsSpan()));
        }

        [Fact]
        public void ExtractTraceContext_WithByteArrayTraceState_ShouldParseCorrectly()
        {
            // Arrange
            var traceStateBytes = Encoding.UTF8.GetBytes("congo=t61rcWkgMzE");
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "tracestate", traceStateBytes }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.TraceState.Should().Be("congo=t61rcWkgMzE");
        }

        [Fact]
        public void ExtractTraceContext_WithNonStringNonByteArrayType_ShouldCallToString()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", new TraceParentWrapper("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01") }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().NotBe(default(ActivityContext));
            context.TraceId.Should().Be(ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan()));
        }

        [Fact]
        public void ExtractTraceContext_WithTraceFlagsZero_ShouldSetNoneFlags()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.TraceFlags.Should().Be(ActivityTraceFlags.None);
        }

        [Fact]
        public void InjectTraceContext_WithValidActivity_ShouldInjectTraceParent()
        {
            // Arrange
            var headers = new Dictionary<string, object>();
            using var activity = _activitySource.StartActivity("test-activity", ActivityKind.Internal);

            // Act
            TraceContextPropagation.InjectTraceContext(headers, activity);

            // Assert
            headers.Should().ContainKey("traceparent");
            headers["traceparent"].Should().BeOfType<string>();
            var traceparent = headers["traceparent"] as string;
            traceparent.Should().StartWith("00-");
            traceparent.Split('-').Should().HaveCount(4);
        }

        [Fact]
        public void InjectTraceContext_WithActivityWithTraceState_ShouldInjectTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, object>();
            using var activity = _activitySource.StartActivity("test-activity", ActivityKind.Internal);
            activity.TraceStateString = "congo=t61rcWkgMzE";

            // Act
            TraceContextPropagation.InjectTraceContext(headers, activity);

            // Assert
            headers.Should().ContainKey("traceparent");
            headers.Should().ContainKey("tracestate");
            headers["tracestate"].Should().Be("congo=t61rcWkgMzE");
        }

        [Fact]
        public void InjectTraceContext_WithNullHeaders_ShouldNotThrow()
        {
            // Arrange
            using var activity = _activitySource.StartActivity("test-activity", ActivityKind.Internal);

            // Act
            Action act = () => TraceContextPropagation.InjectTraceContext(null, activity);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectTraceContext_WithNullActivity_ShouldNotThrow()
        {
            // Arrange
            var headers = new Dictionary<string, object>();

            // Act
            Action act = () => TraceContextPropagation.InjectTraceContext(headers, null);

            // Assert
            act.Should().NotThrow();
            headers.Should().BeEmpty();
        }

        [Fact]
        public void InjectTraceContext_WithBothNull_ShouldNotThrow()
        {
            // Act
            Action act = () => TraceContextPropagation.InjectTraceContext(null, null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectTraceContext_WithActivityWithoutTraceState_ShouldNotInjectTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, object>();
            using var activity = _activitySource.StartActivity("test-activity", ActivityKind.Internal);

            // Act
            TraceContextPropagation.InjectTraceContext(headers, activity);

            // Assert
            headers.Should().ContainKey("traceparent");
            headers.Should().NotContainKey("tracestate");
        }

        [Fact]
        public void InjectTraceContext_WithActivityWithEmptyTraceState_ShouldNotInjectTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, object>();
            using var activity = _activitySource.StartActivity("test-activity", ActivityKind.Internal);
            if (activity != null)
            {
                activity.TraceStateString = "";
            }

            // Act
            TraceContextPropagation.InjectTraceContext(headers, activity);

            // Assert
            headers.Should().ContainKey("traceparent");
            headers.Should().NotContainKey("tracestate");
        }

        [Fact]
        public void InjectTraceContext_WithActivityWithWhitespaceTraceState_ShouldNotInjectTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, object>();
            using var activity = _activitySource.StartActivity("test-activity", ActivityKind.Internal);
            if (activity != null)
            {
                activity.TraceStateString = "   ";
            }

            // Act
            TraceContextPropagation.InjectTraceContext(headers, activity);

            // Assert
            headers.Should().ContainKey("traceparent");
            headers.Should().NotContainKey("tracestate");
        }

        [Fact]
        public void InjectAndExtract_RoundTrip_ShouldPreserveContext()
        {
            // Arrange
            using var originalActivity = _activitySource.StartActivity("test-activity", ActivityKind.Internal);
            // Note: TraceStateString might not be properly set on all .NET versions
            // So we'll test the round-trip without it first
            
            var headers = new Dictionary<string, object>();

            // Act - Inject
            TraceContextPropagation.InjectTraceContext(headers, originalActivity);
            
            // Act - Extract
            var extractedContext = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            extractedContext.TraceId.Should().Be(originalActivity.TraceId);
            extractedContext.SpanId.Should().Be(originalActivity.SpanId);
            extractedContext.TraceFlags.Should().Be(originalActivity.ActivityTraceFlags);
        }

        [Fact]
        public void InjectAndExtract_RoundTrip_WithTraceState_ShouldPreserveTraceState()
        {
            // Arrange - Create activity with parent context that has tracestate
            var parentTraceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            var parentContext = new ActivityContext(parentTraceId, parentSpanId, ActivityTraceFlags.Recorded, "vendor1=value1");
            
            using var originalActivity = _activitySource.StartActivity("test-activity", ActivityKind.Internal, parentContext);
            
            var headers = new Dictionary<string, object>();

            // Act - Inject
            TraceContextPropagation.InjectTraceContext(headers, originalActivity);
            
            // Act - Extract
            var extractedContext = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            extractedContext.TraceId.Should().Be(originalActivity.TraceId);
            extractedContext.SpanId.Should().Be(originalActivity.SpanId);
            extractedContext.TraceFlags.Should().Be(originalActivity.ActivityTraceFlags);
            // TraceState should be preserved if it was set
            if (!string.IsNullOrEmpty(originalActivity.TraceStateString))
            {
                extractedContext.TraceState.Should().Be(originalActivity.TraceStateString);
            }
        }

        [Fact]
        public void ExtractTraceContext_WithNullTraceState_ShouldNotIncludeTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "tracestate", null }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().NotBe(default(ActivityContext));
            context.TraceState.Should().BeNull();
        }

        [Fact]
        public void ExtractTraceContext_WithEmptyTraceState_ShouldIncludeEmptyTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "tracestate", "" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().NotBe(default(ActivityContext));
            context.TraceState.Should().Be("");
        }

        // Helper class for testing ToString conversion
        private class TraceParentWrapper
        {
            private readonly string _value;
            
            public TraceParentWrapper(string value)
            {
                _value = value;
            }

            public override string ToString()
            {
                return _value;
            }
        }
    }
}
