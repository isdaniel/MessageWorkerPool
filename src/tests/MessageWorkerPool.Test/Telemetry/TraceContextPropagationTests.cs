using FluentAssertions;
using MessageWorkerPool.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace MessageWorkerPool.Test.Telemetry
{
    public class TraceContextPropagationTests
    {
        #region ExtractTraceContext - IDictionary<string, object> Tests

        [Fact]
        public void ExtractTraceContext_ObjectDict_WithValidTraceParent_ShouldReturnActivityContext()
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
        public void ExtractTraceContext_ObjectDict_WithTraceParentAndTraceState_ShouldReturnContextWithTraceState()
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
        public void ExtractTraceContext_ObjectDict_WithNullHeaders_ShouldReturnDefaultContext()
        {
            // Act
            var context = TraceContextPropagation.ExtractTraceContext((IDictionary<string, object>)null);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_ObjectDict_WithEmptyHeaders_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>();

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_ObjectDict_WithMissingTraceParent_ShouldReturnDefaultContext()
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
        public void ExtractTraceContext_ObjectDict_WithNullTraceParent_ShouldReturnDefaultContext()
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
        public void ExtractTraceContext_ObjectDict_WithEmptyTraceParent_ShouldReturnDefaultContext()
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
        public void ExtractTraceContext_ObjectDict_WithWhitespaceTraceParent_ShouldReturnDefaultContext()
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
        public void ExtractTraceContext_ObjectDict_WithInvalidFormat_TooFewParts_ShouldReturnDefaultContext()
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
        public void ExtractTraceContext_ObjectDict_WithInvalidFormat_TooManyParts_ShouldReturnDefaultContext()
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
        public void ExtractTraceContext_ObjectDict_WithInvalidTraceId_ShouldReturnDefaultContext()
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
        public void ExtractTraceContext_ObjectDict_WithInvalidSpanId_ShouldReturnDefaultContext()
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
        public void ExtractTraceContext_ObjectDict_WithInvalidTraceFlags_ShouldReturnDefaultContext()
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
        public void ExtractTraceContext_ObjectDict_WithByteArrayTraceParent_ShouldParseCorrectly()
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
        public void ExtractTraceContext_ObjectDict_WithByteArrayTraceState_ShouldParseCorrectly()
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
        public void ExtractTraceContext_ObjectDict_WithNonStringNonByteArrayType_ShouldCallToString()
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
        public void ExtractTraceContext_ObjectDict_WithTraceFlagsZero_ShouldSetNoneFlags()
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
        public void ExtractTraceContext_ObjectDict_WithNullTraceState_ShouldNotIncludeTraceState()
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
        public void ExtractTraceContext_ObjectDict_WithEmptyTraceState_ShouldIncludeEmptyTraceState()
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

        #endregion

        #region ExtractTraceContext - IDictionary<string, string> Tests

        [Fact]
        public void ExtractTraceContext_StringDict_WithValidTraceParent_ShouldReturnActivityContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>
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
        public void ExtractTraceContext_StringDict_WithTraceParentAndTraceState_ShouldReturnContextWithTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, string>
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
        public void ExtractTraceContext_StringDict_WithNullHeaders_ShouldReturnDefaultContext()
        {
            // Act
            var context = TraceContextPropagation.ExtractTraceContext((IDictionary<string, string>)null);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithEmptyHeaders_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>();

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithMissingTraceParent_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "other-header", "value" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithNullTraceParent_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", null }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithEmptyTraceParent_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", "" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithWhitespaceTraceParent_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", "   " }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithInvalidFormat_TooFewParts_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithInvalidFormat_TooManyParts_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01-extra" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithInvalidTraceId_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", "00-invalid-trace-id-b7ad6b7169203331-01" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithInvalidSpanId_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-invalid-span-01" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithInvalidTraceFlags_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-zz" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithTraceFlagsZero_ShouldSetNoneFlags()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.TraceFlags.Should().Be(ActivityTraceFlags.None);
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithNullTraceState_ShouldNotIncludeTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, string>
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
        public void ExtractTraceContext_StringDict_WithEmptyTraceState_ShouldIncludeEmptyTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, string>
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

        [Fact]
        public void ExtractTraceContext_StringDict_WithMultipleTraceStateEntries_ShouldPreserveFullTraceState()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "tracestate", "vendor1=value1,vendor2=value2" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().NotBe(default(ActivityContext));
            context.TraceState.Should().Be("vendor1=value1,vendor2=value2");
        }

        #endregion

        #region Cross-Overload Compatibility Tests

        [Fact]
        public void ExtractTraceContext_BothOverloads_WithSameValidData_ShouldReturnSameContext()
        {
            // Arrange
            var objectHeaders = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "tracestate", "vendor=value" }
            };

            var stringHeaders = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "tracestate", "vendor=value" }
            };

            // Act
            var contextFromObjectDict = TraceContextPropagation.ExtractTraceContext(objectHeaders);
            var contextFromStringDict = TraceContextPropagation.ExtractTraceContext(stringHeaders);

            // Assert
            contextFromObjectDict.TraceId.Should().Be(contextFromStringDict.TraceId);
            contextFromObjectDict.SpanId.Should().Be(contextFromStringDict.SpanId);
            contextFromObjectDict.TraceFlags.Should().Be(contextFromStringDict.TraceFlags);
            contextFromObjectDict.TraceState.Should().Be(contextFromStringDict.TraceState);
        }

        [Fact]
        public void ExtractTraceContext_BothOverloads_WithNullHeaders_ShouldReturnDefaultContext()
        {
            // Act
            var contextFromObjectDict = TraceContextPropagation.ExtractTraceContext((IDictionary<string, object>)null);
            var contextFromStringDict = TraceContextPropagation.ExtractTraceContext((IDictionary<string, string>)null);

            // Assert
            contextFromObjectDict.Should().Be(default(ActivityContext));
            contextFromStringDict.Should().Be(default(ActivityContext));
            contextFromObjectDict.Should().Be(contextFromStringDict);
        }

        [Fact]
        public void ExtractTraceContext_BothOverloads_WithEmptyHeaders_ShouldReturnDefaultContext()
        {
            // Arrange
            var objectHeaders = new Dictionary<string, object>();
            var stringHeaders = new Dictionary<string, string>();

            // Act
            var contextFromObjectDict = TraceContextPropagation.ExtractTraceContext(objectHeaders);
            var contextFromStringDict = TraceContextPropagation.ExtractTraceContext(stringHeaders);

            // Assert
            contextFromObjectDict.Should().Be(default(ActivityContext));
            contextFromStringDict.Should().Be(default(ActivityContext));
            contextFromObjectDict.Should().Be(contextFromStringDict);
        }

        #endregion

        #region Edge Cases and Robustness Tests

        [Fact]
        public void ExtractTraceContext_ObjectDict_WithIntegerValue_ShouldCallToString()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", 123456 }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert - Should fail to parse since "123456" is not a valid traceparent
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithCaseVariations_ShouldBeCaseSensitive()
        {
            // Arrange - traceparent header name should be case-sensitive per W3C spec
            var headers = new Dictionary<string, string>
            {
                { "TraceParent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert - Should not find the header with different case
            context.Should().Be(default(ActivityContext));
        }

        [Fact]
        public void ExtractTraceContext_ObjectDict_WithVeryLongTraceState_ShouldPreserveFullValue()
        {
            // Arrange
            var longTraceState = string.Join(",", Enumerable.Range(1, 100).Select(i => $"vendor{i}=value{i}"));
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "tracestate", longTraceState }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().NotBe(default(ActivityContext));
            context.TraceState.Should().Be(longTraceState);
        }

        [Fact]
        public void ExtractTraceContext_ObjectDict_WithAllZeroTraceId_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-00000000000000000000000000000000-b7ad6b7169203331-01" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert - .NET treats all-zero trace ID as invalid and returns default context
            // This is a .NET implementation detail where ActivityContext with all-zero trace ID
            // is considered invalid and gets normalized to a default/invalid state
            context.TraceId.ToString().Should().Be("00000000000000000000000000000000");
            context.SpanId.ToString().Should().Be("0000000000000000");
        }

        [Fact]
        public void ExtractTraceContext_ObjectDict_WithAllZeroSpanId_ShouldReturnDefaultContext()
        {
            // Arrange
            var headers = new Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-0000000000000000-01" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert - .NET treats all-zero span ID as invalid and returns default context
            // This is a .NET implementation detail where ActivityContext with all-zero span ID
            // is considered invalid and gets normalized to a default/invalid state
            context.TraceId.ToString().Should().Be("00000000000000000000000000000000");
            context.SpanId.ToString().Should().Be("0000000000000000");
        }

        [Fact]
        public void ExtractTraceContext_StringDict_WithSpecialCharactersInTraceState_ShouldPreserve()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "tracestate", "vendor1=key:value/path,vendor2=abc@123" }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert
            context.Should().NotBe(default(ActivityContext));
            context.TraceState.Should().Be("vendor1=key:value/path,vendor2=abc@123");
        }

        [Fact]
        public void ExtractTraceContext_ObjectDict_WithByteArrayContainingInvalidUtf8_ShouldHandleGracefully()
        {
            // Arrange
            var invalidUtf8Bytes = new byte[] { 0xFF, 0xFE, 0xFD };
            var headers = new Dictionary<string, object>
            {
                { "traceparent", invalidUtf8Bytes }
            };

            // Act
            var context = TraceContextPropagation.ExtractTraceContext(headers);

            // Assert - Should return default context since the bytes can't be decoded to valid traceparent
            context.Should().Be(default(ActivityContext));
        }

        #endregion

        #region Helper Classes

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

        #endregion
    }
}
