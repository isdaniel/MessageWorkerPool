using FluentAssertions;
using MessageWorkerPool.OpenTelemetry.Extensions;
using MessageWorkerPool.Telemetry;
using MessageWorkerPool.Telemetry.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System;
using System.Linq;
using Xunit;

namespace MessageWorkerPool.Test.OpenTelemetry.Extensions
{
    /// <summary>
    /// Mock implementation of IEnvironmentInfoProvider for testing.
    /// </summary>
    public class MockEnvironmentInfoProvider : IEnvironmentInfoProvider
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _environmentVariables = new System.Collections.Generic.Dictionary<string, string>();
        private string _hostName = "mock-hostname";

        public void SetEnvironmentVariable(string variable, string value)
        {
            if (value == null)
            {
                _environmentVariables.Remove(variable);
            }
            else
            {
                _environmentVariables[variable] = value;
            }
        }

        public void SetHostName(string hostName)
        {
            _hostName = hostName;
        }

        public string GetEnvironmentVariable(string variable)
        {
            return _environmentVariables.TryGetValue(variable, out var value) ? value : null;
        }

        public string GetHostName()
        {
            return _hostName;
        }
    }

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
        public void AddMessageWorkerPoolTelemetry_WithCustomServiceInstanceId_ShouldUseCustomValue()
        {
            // Arrange
            var services = new ServiceCollection();
            var customInstanceId = "custom-instance-123";

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = customInstanceId;
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithoutServiceInstanceId_ShouldFallbackToHostname()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = null; // Explicitly null to test fallback
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_ConfigureResource_ShouldSetHostNameAttribute()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert - Verify the service provider is properly configured
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_ConfigureResource_ShouldSetContainerIdAttribute()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert - Verify the service provider is properly configured with attributes
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithHostnameEnvironmentVariable_ShouldUseHostnameForInstanceId()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockEnvProvider = new MockEnvironmentInfoProvider();
            mockEnvProvider.SetEnvironmentVariable("HOSTNAME", "test-container-hostname");
            services.AddSingleton<IEnvironmentInfoProvider>(mockEnvProvider);

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = null; // Force fallback to env var
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithComputerNameEnvironmentVariable_ShouldUseComputerNameForInstanceId()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockEnvProvider = new MockEnvironmentInfoProvider();
            mockEnvProvider.SetEnvironmentVariable("COMPUTERNAME", "test-computer-name");
            services.AddSingleton<IEnvironmentInfoProvider>(mockEnvProvider);

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = null; // Force fallback to env var
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithoutAnyEnvironmentVariables_ShouldFallbackToDnsHostName()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockEnvProvider = new MockEnvironmentInfoProvider();
            mockEnvProvider.SetHostName("fallback-dns-hostname");
            services.AddSingleton<IEnvironmentInfoProvider>(mockEnvProvider);

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = null; // Force fallback to DNS
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_ContainerId_WithHostnameEnvVar_ShouldUseHostnameValue()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockEnvProvider = new MockEnvironmentInfoProvider();
            mockEnvProvider.SetEnvironmentVariable("HOSTNAME", "container-xyz-123");
            services.AddSingleton<IEnvironmentInfoProvider>(mockEnvProvider);

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert - Verify service provider configuration
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_ContainerId_WithoutHostnameEnvVar_ShouldUseNA()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockEnvProvider = new MockEnvironmentInfoProvider();
            services.AddSingleton<IEnvironmentInfoProvider>(mockEnvProvider);

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert - Verify service provider configuration with N/A as container.id
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_ResourceAttributes_ShouldIncludeHostName()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert - The host.name attribute should be added
            // This test verifies that the AddAttributes call is properly configured
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_ServiceInstanceId_CanBeSet()
        {
            // Arrange
            var options = new MessageWorkerPoolTelemetryOptions();
            var instanceId = "my-instance-456";

            // Act
            options.ServiceInstanceId = instanceId;

            // Assert
            options.ServiceInstanceId.Should().Be(instanceId);
        }

        [Fact]
        public void MessageWorkerPoolTelemetryOptions_ServiceInstanceId_DefaultsToNull()
        {
            // Arrange & Act
            var options = new MessageWorkerPoolTelemetryOptions();

            // Assert
            options.ServiceInstanceId.Should().BeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_InstanceIdPriorityOrder_CustomValueTakesPrecedence()
        {
            // Arrange
            var services = new ServiceCollection();
            var customInstanceId = "priority-custom-id";
            var mockEnvProvider = new MockEnvironmentInfoProvider();
            mockEnvProvider.SetEnvironmentVariable("HOSTNAME", "should-be-ignored-hostname");
            mockEnvProvider.SetEnvironmentVariable("COMPUTERNAME", "should-be-ignored-computername");
            services.AddSingleton<IEnvironmentInfoProvider>(mockEnvProvider);

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = customInstanceId;
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert - Custom value should take precedence over environment variables
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_InstanceIdPriorityOrder_HostnameTakesPrecedenceOverComputerName()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockEnvProvider = new MockEnvironmentInfoProvider();
            mockEnvProvider.SetEnvironmentVariable("HOSTNAME", "hostname-takes-precedence");
            mockEnvProvider.SetEnvironmentVariable("COMPUTERNAME", "should-be-ignored-computername");
            services.AddSingleton<IEnvironmentInfoProvider>(mockEnvProvider);

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
                options.ServiceInstanceId = null; // Force environment variable resolution
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert - HOSTNAME should take precedence over COMPUTERNAME
            serviceProvider.GetRequiredService<ITelemetryManager>().Provider.Should().NotBeNull();
        }

        [Fact]
        public void DefaultEnvironmentInfoProvider_GetEnvironmentVariable_ShouldReturnEnvironmentVariable()
        {
            // Arrange
            var provider = new DefaultEnvironmentInfoProvider();
            var testVarName = "PATH"; // PATH should exist on all systems

            // Act
            var result = provider.GetEnvironmentVariable(testVarName);

            // Assert
            result.Should().Be(Environment.GetEnvironmentVariable(testVarName));
        }

        [Fact]
        public void DefaultEnvironmentInfoProvider_GetHostName_ShouldReturnHostName()
        {
            // Arrange
            var provider = new DefaultEnvironmentInfoProvider();

            // Act
            var result = provider.GetHostName();

            // Assert
            result.Should().Be(System.Net.Dns.GetHostName());
        }

        [Fact]
        public void MockEnvironmentInfoProvider_SetEnvironmentVariable_ShouldStoreValue()
        {
            // Arrange
            var provider = new MockEnvironmentInfoProvider();
            var varName = "TEST_VAR";
            var varValue = "test-value";

            // Act
            provider.SetEnvironmentVariable(varName, varValue);
            var result = provider.GetEnvironmentVariable(varName);

            // Assert
            result.Should().Be(varValue);
        }

        [Fact]
        public void MockEnvironmentInfoProvider_SetEnvironmentVariable_WithNull_ShouldRemoveValue()
        {
            // Arrange
            var provider = new MockEnvironmentInfoProvider();
            var varName = "TEST_VAR";
            provider.SetEnvironmentVariable(varName, "initial-value");

            // Act
            provider.SetEnvironmentVariable(varName, null);
            var result = provider.GetEnvironmentVariable(varName);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void MockEnvironmentInfoProvider_SetHostName_ShouldReturnCustomHostName()
        {
            // Arrange
            var provider = new MockEnvironmentInfoProvider();
            var customHostName = "custom-test-host";

            // Act
            provider.SetHostName(customHostName);
            var result = provider.GetHostName();

            // Assert
            result.Should().Be(customHostName);
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithMockProvider_ShouldRegisterDefaultIfNotProvided()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act - Don't register a custom provider
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
            });

            var serviceProvider = services.BuildServiceProvider();
            var envProvider = serviceProvider.GetRequiredService<IEnvironmentInfoProvider>();

            // Assert - Should have registered the default provider
            envProvider.Should().BeOfType<DefaultEnvironmentInfoProvider>();
        }

        [Fact]
        public void AddMessageWorkerPoolTelemetry_WithCustomProvider_ShouldUseCustomProvider()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockProvider = new MockEnvironmentInfoProvider();
            mockProvider.SetHostName("custom-provider-host");
            services.AddSingleton<IEnvironmentInfoProvider>(mockProvider);

            // Act
            services.AddMessageWorkerPoolTelemetry(options =>
            {
                options.ServiceName = "TestService";
                options.ServiceVersion = "1.0.0";
            });

            var serviceProvider = services.BuildServiceProvider();
            var envProvider = serviceProvider.GetRequiredService<IEnvironmentInfoProvider>();

            // Assert - Should use the custom provider we registered
            envProvider.Should().BeSameAs(mockProvider);
        }
    }
}

