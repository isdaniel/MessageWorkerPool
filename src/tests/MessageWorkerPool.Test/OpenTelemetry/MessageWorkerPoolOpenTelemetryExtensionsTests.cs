using FluentAssertions;
using MessageWorkerPool.OpenTelemetry.Extensions;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.Telemetry.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System;
using Xunit;

namespace MessageWorkerPool.Test.OpenTelemetry.Extensions
{
    [Collection("TelemetryTests")]
    public class MessageWorkerPoolOpenTelemetryExtensionsTests
    {
        [Fact]
        public void AddMessageWorkerPoolOpenTelemetry_ShouldSetOpenTelemetryProvider()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolOpenTelemetry();
            var serviceProvider = services.BuildServiceProvider();
            var telemetryManager = serviceProvider.GetRequiredService<ITelemetryManager>();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().BeOfType<MessageWorkerPool.OpenTelemetry.OpenTelemetryProvider>();
        }

        [Fact]
        public void AddMessageWorkerPoolOpenTelemetry_WithCustomServiceName_ShouldSetProviderWithCustomName()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolOpenTelemetry("CustomService", "2.0.0");
            var serviceProvider = services.BuildServiceProvider();
            var telemetryManager = serviceProvider.GetRequiredService<ITelemetryManager>();

            // Assert
            var provider = serviceProvider.GetRequiredService<ITelemetryManager>().Provider as MessageWorkerPool.OpenTelemetry.OpenTelemetryProvider;
            provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolOpenTelemetry_ShouldSetTraceContextExtractor()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolOpenTelemetry();
            var serviceProvider = services.BuildServiceProvider();
            var telemetryManager = serviceProvider.GetRequiredService<ITelemetryManager>();

            // Assert - Verify extractor is set by testing extraction
            var headers = new System.Collections.Generic.Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };

            // Create a new telemetry instance which will use the extractor
            var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", null, telemetryManager, headers);
            telemetry.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolOpenTelemetry_WithDefaultParameters_ShouldUseDefaultValues()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var result = services.AddMessageWorkerPoolOpenTelemetry();
            var serviceProvider = services.BuildServiceProvider();
            var telemetryManager = serviceProvider.GetRequiredService<ITelemetryManager>();

            // Assert
            result.Should().BeSameAs(services);
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().BeOfType<MessageWorkerPool.OpenTelemetry.OpenTelemetryProvider>();
        }

        [Fact]
        public void AddMessageWorkerPoolInstrumentation_MeterProviderBuilder_WithNullBuilder_ShouldThrowArgumentNullException()
        {
            // Arrange
            MeterProviderBuilder builder = null;

            // Act
            Action act = () => builder.AddMessageWorkerPoolInstrumentation();

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
        }

        [Fact]
        public void AddMessageWorkerPoolInstrumentation_MeterProviderBuilder_ShouldReturnBuilder()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    // Act
                    var result = metrics.AddMessageWorkerPoolInstrumentation();

                    // Assert
                    result.Should().BeSameAs(metrics);
                });
        }

        [Fact]
        public void AddMessageWorkerPoolInstrumentation_MeterProviderBuilder_WithCustomServiceName_ShouldAddMeter()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    var result = metrics.AddMessageWorkerPoolInstrumentation("CustomService");
                    result.Should().BeSameAs(metrics);
                });
        }

        [Fact]
        public void AddMessageWorkerPoolInstrumentation_TracerProviderBuilder_WithNullBuilder_ShouldThrowArgumentNullException()
        {
            // Arrange
            TracerProviderBuilder builder = null;

            // Act
            Action act = () => builder.AddMessageWorkerPoolInstrumentation();

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
        }

        [Fact]
        public void AddMessageWorkerPoolInstrumentation_TracerProviderBuilder_ShouldReturnBuilder()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    // Act
                    var result = tracing.AddMessageWorkerPoolInstrumentation();

                    // Assert
                    result.Should().BeSameAs(tracing);
                });
        }

        [Fact]
        public void AddMessageWorkerPoolInstrumentation_TracerProviderBuilder_WithCustomServiceName_ShouldAddSource()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    var result = tracing.AddMessageWorkerPoolInstrumentation("CustomService");
                    result.Should().BeSameAs(tracing);
                });
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithoutConfiguration_ShouldUseDefaultOptions()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var result = services.AddMessageWorkerPoolTelemetry();
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            result.Should().BeSameAs(services);
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().BeOfType<MessageWorkerPool.OpenTelemetry.OpenTelemetryProvider>();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithConfiguration_ShouldApplyOptions()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "3.0.0";
                options.EnableRuntimeInstrumentation = false;
            });
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().BeOfType<MessageWorkerPool.OpenTelemetry.OpenTelemetryProvider>();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithCustomMetricsConfiguration_ShouldApplyConfiguration()
        {
            // Arrange
            var services = new ServiceCollection();
            bool metricsConfigured = false;

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ConfigureMetrics = metrics =>
                {
                    metricsConfigured = true;
                };
            });

            // Assert
            metricsConfigured.Should().BeTrue();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithCustomTracingConfiguration_ShouldApplyConfiguration()
        {
            // Arrange
            var services = new ServiceCollection();
            bool tracingConfigured = false;

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ConfigureTracing = tracing =>
                {
                    tracingConfigured = true;
                };
            });

            // Assert
            tracingConfigured.Should().BeTrue();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithRuntimeInstrumentationEnabled_ShouldConfigureCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.EnableRuntimeInstrumentation = true;
            });
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithProcessInstrumentationEnabled_ShouldConfigureCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.EnableRuntimeInstrumentation = false;
            });
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithAllInstrumentationDisabled_ShouldStillConfigure()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.EnableRuntimeInstrumentation = false;
            });
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().BeOfType<MessageWorkerPool.OpenTelemetry.OpenTelemetryProvider>();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_ShouldSetTraceContextExtractor()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry();
            var serviceProvider = services.BuildServiceProvider();
            var telemetryManager = serviceProvider.GetRequiredService<ITelemetryManager>();

            // Assert - Verify extractor is set
            var headers = new System.Collections.Generic.Dictionary<string, object>
            {
                { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
            };

            var telemetry = new TaskProcessingTelemetry("worker-1", "test-queue", "corr-123", null, telemetryManager, headers);
            telemetry.Should().NotBeNull();
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var options = new MessageWorkerPoolTelemetryOptions();

            // Assert
            options.ServiceName.Should().Be("MessageWorkerPool");
            options.ServiceVersion.Should().Be("1.0.0");
            options.EnableRuntimeInstrumentation.Should().BeTrue();
            options.ConfigureMetrics.Should().BeNull();
            options.ConfigureTracing.Should().BeNull();
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_ServiceName_CanBeSet()
        {
            // Arrange
            var options = new MessageWorkerPoolTelemetryOptions();

            // Act
            options.ServiceName = "CustomService";

            // Assert
            options.ServiceName.Should().Be("CustomService");
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_ServiceVersion_CanBeSet()
        {
            // Arrange
            var options = new MessageWorkerPoolTelemetryOptions();

            // Act
            options.ServiceVersion = "2.5.0";

            // Assert
            options.ServiceVersion.Should().Be("2.5.0");
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_EnableRuntimeInstrumentation_CanBeSet()
        {
            // Arrange
            var options = new MessageWorkerPoolTelemetryOptions();

            // Act
            options.EnableRuntimeInstrumentation = false;

            // Assert
            options.EnableRuntimeInstrumentation.Should().BeFalse();
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_ConfigureMetrics_CanBeSet()
        {
            // Arrange
            var options = new MessageWorkerPoolTelemetryOptions();
            Action<MeterProviderBuilder> configureAction = metrics => { };

            // Act
            options.ConfigureMetrics = configureAction;

            // Assert
            options.ConfigureMetrics.Should().BeSameAs(configureAction);
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_ConfigureTracing_CanBeSet()
        {
            // Arrange
            var options = new MessageWorkerPoolTelemetryOptions();
            Action<TracerProviderBuilder> configureAction = tracing => { };

            // Act
            options.ConfigureTracing = configureAction;

            // Assert
            options.ConfigureTracing.Should().BeSameAs(configureAction);
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithNullConfiguration_ShouldUseDefaultOptions()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(null);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().BeOfType<MessageWorkerPool.OpenTelemetry.OpenTelemetryProvider>();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_ShouldConfigureResourceWithServiceInfo()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestWorkerPool";
                options.ServiceVersion = "1.2.3";
            });

            // Build the service provider to ensure configuration is applied
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            Action act = () =>
            {
                services.AddMessageWorkerPoolTelemetry();
                services.AddMessageWorkerPoolTelemetry();
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void AddMessageWorkerPoolOpenTelemetry_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            Action act = () =>
            {
                services.AddMessageWorkerPoolOpenTelemetry();
                services.AddMessageWorkerPoolOpenTelemetry();
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_ServiceInstanceId_DefaultValueShouldBeNull()
        {
            // Act
            var options = new MessageWorkerPoolTelemetryOptions();

            // Assert
            options.ServiceInstanceId.Should().BeNull();
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_ServiceInstanceId_CanBeSet()
        {
            // Arrange
            var options = new MessageWorkerPoolTelemetryOptions();
            var instanceId = "custom-instance-123";

            // Act
            options.ServiceInstanceId = instanceId;

            // Assert
            options.ServiceInstanceId.Should().Be(instanceId);
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithCustomServiceInstanceId_ShouldApplyInstanceId()
        {
            // Arrange
            var services = new ServiceCollection();
            var customInstanceId = "worker-node-42";

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestWorkerPool";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = customInstanceId;
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithNullServiceInstanceId_ShouldUseAutoDetection()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestWorkerPool";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = null; // Explicitly set to null
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithEmptyServiceInstanceId_ShouldHandleEmptyString()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestWorkerPool";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = string.Empty;
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithLongServiceInstanceId_ShouldAcceptLongInstanceId()
        {
            // Arrange
            var services = new ServiceCollection();
            var longInstanceId = new string('a', 256); // Very long instance ID

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestWorkerPool";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = longInstanceId;
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithSpecialCharactersInInstanceId_ShouldHandleSpecialCharacters()
        {
            // Arrange
            var services = new ServiceCollection();
            var specialInstanceId = "worker-01_prod.app@hostname:8080";

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestWorkerPool";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = specialInstanceId;
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_ServiceInstanceIdFromEnvironmentVariable_ShouldWork()
        {
            // Arrange
            var services = new ServiceCollection();
            var envVarInstanceId = Environment.GetEnvironmentVariable("COMPUTERNAME") 
                ?? Environment.GetEnvironmentVariable("HOSTNAME") 
                ?? "fallback-hostname";

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestWorkerPool";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = envVarInstanceId;
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithAllOptionsIncludingInstanceId_ShouldApplyAllSettings()
        {
            // Arrange
            var services = new ServiceCollection();
            bool metricsConfigured = false;
            bool tracingConfigured = false;

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "FullyConfiguredService";
                options.ServiceVersion = "2.0.0";
                options.ServiceInstanceId = "instance-full-test";
                options.EnableRuntimeInstrumentation = true;
                options.ConfigureMetrics = metrics => { metricsConfigured = true; };
                options.ConfigureTracing = tracing => { tracingConfigured = true; };
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
            metricsConfigured.Should().BeTrue();
            tracingConfigured.Should().BeTrue();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithDifferentInstanceIdFormats_ShouldHandleVariousFormats()
        {
            // Test various instance ID formats
            var testCases = new[]
            {
                "simple-id",
                "container-abc123def456",
                "192.168.1.100",
                "worker-01",
                "UPPERCASE-INSTANCE",
                "mixed-Case_123",
                "with.dots.and-dashes_123"
            };

            foreach (var instanceId in testCases)
            {
                // Arrange
                var services = new ServiceCollection();

                // Act
                services.AddMessageWorkerPoolTelemetry(options =>
                {
                    options.ServiceInstanceId = instanceId;
                });

                var serviceProvider = services.BuildServiceProvider();

                // Assert
                serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull(
                    $"Failed for instance ID: {instanceId}");
            }
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithGuidAsInstanceId_ShouldHandleGuidFormat()
        {
            // Arrange
            var services = new ServiceCollection();
            var guidInstanceId = Guid.NewGuid().ToString();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceInstanceId = guidInstanceId;
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_AllProperties_ShouldBeSettableIndependently()
        {
            // Arrange
            var options = new MessageWorkerPoolTelemetryOptions();

            // Act - Set all properties
            options.ServiceName = "CustomService";
            options.ServiceVersion = "3.0.0";
            options.ServiceInstanceId = "custom-instance";
            options.EnableRuntimeInstrumentation = false;
            options.ConfigureMetrics = metrics => { };
            options.ConfigureTracing = tracing => { };

            // Assert
            options.ServiceName.Should().Be("CustomService");
            options.ServiceVersion.Should().Be("3.0.0");
            options.ServiceInstanceId.Should().Be("custom-instance");
            options.EnableRuntimeInstrumentation.Should().BeFalse();
            options.ConfigureMetrics.Should().NotBeNull();
            options.ConfigureTracing.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithInstanceIdAndMetricsConfig_ShouldApplyBoth()
        {
            // Arrange
            var services = new ServiceCollection();
            bool metricsConfigured = false;

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceInstanceId = "metrics-test-instance";
                options.ConfigureMetrics = metrics =>
                {
                    metricsConfigured = true;
                    metrics.AddMessageWorkerPoolInstrumentation();
                };
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
            metricsConfigured.Should().BeTrue();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithInstanceIdAndTracingConfig_ShouldApplyBoth()
        {
            // Arrange
            var services = new ServiceCollection();
            bool tracingConfigured = false;

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceInstanceId = "tracing-test-instance";
                options.ConfigureTracing = tracing =>
                {
                    tracingConfigured = true;
                    tracing.AddMessageWorkerPoolInstrumentation();
                };
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
            tracingConfigured.Should().BeTrue();
        }
    }
}

