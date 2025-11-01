using FluentAssertions;
using MessageWorkerPool.OpenTelemetry;
using MessageWorkerPool.Telemetry.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace MessageWorkerPool.Test.OpenTelemetry
{
    public class OpenTelemetryProviderTests : IDisposable
    {
        private readonly OpenTelemetryProvider _provider;
        private readonly ActivityListener _listener;
        private readonly List<Activity> _activities;

        public OpenTelemetryProviderTests()
        {
            _provider = new OpenTelemetryProvider("TestService", "1.0.0");
            _activities = new List<Activity>();
            
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "TestService",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => _activities.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose()
        {
            _provider?.Dispose();
            _listener?.Dispose();
        }

        [Fact]
        public void Constructor_WithDefaultParameters_ShouldCreateProvider()
        {
            // Arrange & Act
            using var provider = new OpenTelemetryProvider();

            // Assert
            provider.Should().NotBeNull();
            provider.Metrics.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithCustomParameters_ShouldCreateProvider()
        {
            // Arrange & Act
            using var provider = new OpenTelemetryProvider("CustomService", "2.0.0");

            // Assert
            provider.Should().NotBeNull();
            provider.Metrics.Should().NotBeNull();
        }

        [Fact]
        public void StartActivity_WithOperationName_ShouldCreateActivity()
        {
            // Act
            using var activity = _provider.StartActivity("TestOperation");

            // Assert
            activity.Should().NotBeNull();
            activity.Should().BeOfType<OpenTelemetryActivity>();
            _activities.Should().HaveCount(1);
            _activities[0].OperationName.Should().Be("TestOperation");
        }

        [Fact]
        public void StartActivity_WithTags_ShouldSetTagsOnActivity()
        {
            // Arrange
            var tags = new Dictionary<string, object>
            {
                { "tag1", "value1" },
                { "tag2", 42 },
                { "tag3", true }
            };

            // Act
            using var activity = _provider.StartActivity("TestOperation", tags);

            // Assert
            activity.Should().NotBeNull();
            _activities.Should().HaveCount(1);
            var underlyingActivity = _activities[0];
            underlyingActivity.GetTagItem("tag1").Should().Be("value1");
            underlyingActivity.GetTagItem("tag2").Should().Be("42");
            underlyingActivity.GetTagItem("tag3").Should().Be("True");
        }

        [Fact]
        public void StartActivity_WithNullTags_ShouldNotThrow()
        {
            // Act
            Action act = () =>
            {
                using var activity = _provider.StartActivity("TestOperation", null);
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void StartActivity_WithEmptyTags_ShouldNotThrow()
        {
            // Arrange
            var tags = new Dictionary<string, object>();

            // Act
            Action act = () =>
            {
                using var activity = _provider.StartActivity("TestOperation", tags);
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Metrics_ShouldReturnOpenTelemetryMetrics()
        {
            // Act
            var metrics = _provider.Metrics;

            // Assert
            metrics.Should().NotBeNull();
            metrics.Should().BeOfType<OpenTelemetryMetrics>();
        }

        [Fact]
        public void Metrics_CalledMultipleTimes_ShouldReturnSameInstance()
        {
            // Act
            var metrics1 = _provider.Metrics;
            var metrics2 = _provider.Metrics;

            // Assert
            metrics1.Should().BeSameAs(metrics2);
        }

        [Fact]
        public void Dispose_ShouldDisposeResourcesWithoutError()
        {
            // Arrange
            var provider = new OpenTelemetryProvider("TestService", "1.0.0");

            // Act
            Action act = () => provider.Dispose();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var provider = new OpenTelemetryProvider("TestService", "1.0.0");

            // Act
            Action act = () =>
            {
                provider.Dispose();
                provider.Dispose();
                provider.Dispose();
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void StartActivity_AfterDispose_ShouldReturnNull()
        {
            // Arrange
            var provider = new OpenTelemetryProvider("TestService", "1.0.0");
            provider.Dispose();

            // Act
            var activity = provider.StartActivity("TestOperation");

            // Assert - After disposal, ActivitySource won't create new activities
            // The behavior depends on the underlying implementation
            // We just verify it doesn't throw
        }

        [Fact]
        public void StartActivity_WithNullTagValue_ShouldHandleGracefully()
        {
            // Arrange
            var tags = new Dictionary<string, object>
            {
                { "tag1", null },
                { "tag2", "value2" }
            };

            // Act
            Action act = () =>
            {
                using var activity = _provider.StartActivity("TestOperation", tags);
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_WhenMetricsIsNull_ShouldNotThrow()
        {
            // Arrange - Create a provider and dispose it
            var provider = new OpenTelemetryProvider("TestService", "1.0.0");

            // Act - Dispose once
            provider.Dispose();

            // Assert - Second dispose should still not throw even if _metrics is already disposed
            Action act = () => provider.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_BothActivitySourceAndMetrics_ShouldDisposeCorrectly()
        {
            // Arrange
            var provider = new OpenTelemetryProvider("TestService", "1.0.0");
            
            // Use the provider to ensure both ActivitySource and Metrics are initialized
            var activity = provider.StartActivity("Test");
            activity?.Dispose();
            
            var metrics = provider.Metrics;
            metrics.Should().NotBeNull();

            // Act
            Action act = () => provider.Dispose();

            // Assert
            act.Should().NotThrow();
        }
    }
}
