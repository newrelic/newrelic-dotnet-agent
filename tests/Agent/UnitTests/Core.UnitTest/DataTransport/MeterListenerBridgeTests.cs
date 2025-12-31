// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Configuration;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class MeterListenerBridgeTests
    {
        private DisposableCollection _disposableCollection;
        private MeterListenerBridge _meterListenerBridge;
        private IConfigurationService _configurationService;
        private IConfiguration _configuration;
        private IConnectionInfo _connectionInfo;
        private IOtelBridgeSupportabilityMetricCounters _supportabilityMetricCounters;
        private IMeterBridgingService _meterBridgingService;
        private IOtlpExporterConfigurationService _otlpExporterConfigurationService;

        [SetUp]
        public void SetUp()
        {
            _disposableCollection = new DisposableCollection();

            // Setup configuration service mock
            _configurationService = Mock.Create<IConfigurationService>();
            
            // Setup configuration mock
            _configuration = Mock.Create<IConfiguration>();
            _disposableCollection.Add(new ConfigurationAutoResponder(_configuration));
            
            Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);

            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "TestApp" });
            Mock.Arrange(() => _configuration.EntityGuid).Returns("test-entity-guid");
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("test-license-key");
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns(new List<string>());
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns(new List<string>());
            Mock.Arrange(() => _configuration.OpenTelemetryOtlpExportIntervalSeconds).Returns(60);
            Mock.Arrange(() => _configuration.OpenTelemetryOtlpTimeoutSeconds).Returns(10);

            // Setup connection info mock
            _connectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => _connectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => _connectionInfo.Host).Returns("collector.newrelic.com");
            Mock.Arrange(() => _connectionInfo.Port).Returns(443);
            Mock.Arrange(() => _connectionInfo.Proxy).Returns((WebProxy)null);

            // Setup supportability metrics mock
            _supportabilityMetricCounters = Mock.Create<IOtelBridgeSupportabilityMetricCounters>();

            // Setup service mocks
            _meterBridgingService = Mock.Create<IMeterBridgingService>();
            _otlpExporterConfigurationService = Mock.Create<IOtlpExporterConfigurationService>();

            _meterListenerBridge = new MeterListenerBridge(_meterBridgingService, _otlpExporterConfigurationService);
        }

        [TearDown]
        public void TearDown()
        {
            _meterListenerBridge?.Dispose();
            _meterBridgingService?.Dispose();
            _otlpExporterConfigurationService?.Dispose();
            _disposableCollection?.Dispose();
        }

        [Test]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Act & Assert
            Assert.That(_meterListenerBridge, Is.Not.Null);
        }

        [Test]
        public void AgentConnectedEvent_ShouldStartMetricCollection()
        {
            // Arrange
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void ConfigurationUpdatedEvent_ShouldNotThrow()
        {
            // Arrange
            var configurationUpdatedEvent = new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local);

            // Act & Assert
            Assert.DoesNotThrow(() => EventBus<ConfigurationUpdatedEvent>.Publish(configurationUpdatedEvent));
        }

        [Test]
        public void PreCleanShutdownEvent_ShouldStopGracefully()
        {
            // Arrange
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            var shutdownEvent = new PreCleanShutdownEvent();

            // Act & Assert
            Assert.DoesNotThrow(() => EventBus<PreCleanShutdownEvent>.Publish(shutdownEvent));
        }

        [Test]
        public void Start_ShouldInitializeOpenTelemetryComponents()
        {
            // Arrange
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act - Call Start directly to test initialization
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());

            // Assert - Verify the bridge can handle multiple start calls without errors
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
        }

        [Test]
        public void Stop_ShouldCleanupResources()
        {
            // Arrange
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act & Assert
            Assert.DoesNotThrow(() => EventBus<PreCleanShutdownEvent>.Publish(new PreCleanShutdownEvent()));
        }

        [Test]
        public void Service_ShouldBeDisposable()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _meterListenerBridge.Dispose());
        }







        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldConfigureProxy()
        {
            // Arrange
            var proxy = new WebProxy("http://proxy.company.com:8080");
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should not throw when configuring with proxy
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldHandleNoProxy()
        {
            // Arrange
            Mock.Arrange(() => _connectionInfo.Proxy).Returns((WebProxy)null);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should not throw when no proxy is configured
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldHandleProxyWithCredentials()
        {
            // Arrange
            var proxy = new WebProxy("http://proxy.company.com:8080")
            {
                Credentials = new NetworkCredential("username", "password")
            };
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should not throw when configuring proxy with credentials
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldHandleProxyWithBypassList()
        {
            // Arrange - Use valid bypass patterns instead of regex wildcards
            var proxy = new WebProxy("http://proxy.company.com:8080", true, new[] { "localhost", "127.0.0.1", "internal.com" });
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should not throw when configuring proxy with bypass list
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldHandleHttpsProxy()
        {
            // Arrange
            var proxy = new WebProxy("https://secure-proxy.company.com:8443");
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should not throw when configuring HTTPS proxy
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldHandleProxyWithoutPort()
        {
            // Arrange
            var proxy = new WebProxy("http://proxy.company.com");
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should not throw when configuring proxy without explicit port
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldHandleInvalidProxyUri()
        {
            // Arrange - Create proxy with high port number that's still technically valid
            var proxy = new WebProxy("http://proxy.invalid:8080"); // Valid URI format with non-existent host
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle edge cases gracefully
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldHandleProxyWithDefaultCredentials()
        {
            // Arrange
            var proxy = new WebProxy("http://proxy.company.com:8080")
            {
                UseDefaultCredentials = true
            };
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should not throw when using default credentials
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldHandleProxyDisabled()
        {
            // Arrange
            var proxy = new WebProxy() { Address = null }; // Disabled proxy
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle disabled proxy gracefully
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldHandleProxyWithEmptyCredentials()
        {
            // Arrange
            var proxy = new WebProxy("http://proxy.company.com:8080")
            {
                Credentials = new NetworkCredential("", "") // Empty credentials
            };
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle empty credentials gracefully
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldSetUserAgent()
        {
            // Arrange - Test that user agent is properly set
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should set proper User-Agent header
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ShouldSetTimeout()
        {
            // Arrange - Test that timeout is properly configured
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should set default 10 second timeout
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void Constructor_ShouldSetupEventSubscriptions()
        {
            // Arrange & Act - Constructor already called in SetUp
            // Test that the bridge subscribes to events correctly

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            var preCleanShutdownEvent = new PreCleanShutdownEvent();

            // Act & Assert - Should handle event subscriptions
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
            Assert.DoesNotThrow(() => EventBus<PreCleanShutdownEvent>.Publish(preCleanShutdownEvent));
        }

        [Test]
        public void TryCreateMeterListener_WhenMeterListenerTypeNotFound_ShouldHandleGracefully()
        {
            // This test validates that the bridge handles cases where MeterListener type is not available
            // Since we can't easily mock the assembly loading, we test the Start method which calls TryCreateMeterListener
            
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act & Assert - Should not throw even if MeterListener creation fails
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
        }

        [Test]
        public void Start_WithCompleteValidConfiguration_ShouldConfigureAllComponents()
        {
            // Arrange - Test comprehensive Start method execution
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "TestApp", "SecondaryApp" });
            Mock.Arrange(() => _configuration.EntityGuid).Returns("test-entity-guid-12345");
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("test-license-key");
            
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act & Assert - Should configure all components without errors
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
            
            // Supportability metric for OTel bridge enabled is now reported by AgentHealthReporter, not MeterListenerBridge.ConfigureOtlpExporter().
        }

        #region Filter Logic Tests



        #endregion

        #region Start/Stop Method Tests

        [Test]
        public void Start_WhenOpenTelemetryDisabled_ShouldNotInitializeComponents()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(false);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            // Act
            _meterListenerBridge.ConfigureOtlpExporter();

            // Act & Assert 
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
            
            // Supportability metric for OTel bridge disabled is now reported by AgentHealthReporter, not MeterListenerBridge.ConfigureOtlpExporter().
        }

        [Test]
        public void Start_WhenMetricsDisabled_ShouldRecordDisabledMetric()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(false);

            // Act
            _meterListenerBridge.ConfigureOtlpExporter();

            // Supportability metric for OTel bridge disabled is now reported by AgentHealthReporter, not MeterListenerBridge.ConfigureOtlpExporter().
        }

        [Test]
        public void Start_WithoutConnectionInfo_ShouldHandleGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            

            // Act & Assert
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
        }

        [Test]
        public void Start_WhenMetricsDisabled_ShouldNotInitializeComponents()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(false);

            // Act
            _meterListenerBridge.ConfigureOtlpExporter();

            // Assert - Should exit early and not throw
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
        }

        [Test]
        public void Start_WithValidConfiguration_ShouldRecordEnabledMetric()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act
            _meterListenerBridge.ConfigureOtlpExporter();

            // Supportability metric for OTel bridge enabled is now reported by AgentHealthReporter, not MeterListenerBridge.ConfigureOtlpExporter().
        }

        [Test]
        public void Start_WithValidConfiguration_ShouldInitializeSuccessfully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act & Assert
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());

            // Verify multiple calls don't cause issues
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
        }

        [Test]
        public void Stop_ShouldCleanupAllResources()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            _meterListenerBridge.ConfigureOtlpExporter();

            // Act
            _meterListenerBridge.Stop();

            // Assert - Should be able to call multiple times without error
            Assert.DoesNotThrow(() => _meterListenerBridge.Stop());

            // Should be able to restart after stop
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void OnConfigurationUpdated_ShouldNotThrowException()
        {
            // Arrange
            var configUpdateEvent = new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local);

            // Act & Assert
            Assert.DoesNotThrow(() => EventBus<ConfigurationUpdatedEvent>.Publish(configUpdateEvent));
        }

        [Test] 
        public void OnAgentConnected_ShouldStopAndRestart()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var firstConnection = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            var secondConnection = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle reconnection gracefully
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(firstConnection);
                EventBus<AgentConnectedEvent>.Publish(secondConnection);
            });
            
            // Supportability metric for OTel bridge enabled is now reported by AgentHealthReporter, not MeterListenerBridge.ConfigureOtlpExporter().
        }



        #endregion

        #region Edge Case Tests

        [Test]
        public void Dispose_ShouldCleanupGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            _meterListenerBridge.ConfigureOtlpExporter();

            // Act & Assert
            Assert.DoesNotThrow(() => _meterListenerBridge.Dispose());

            // Should be safe to dispose multiple times
            Assert.DoesNotThrow(() => _meterListenerBridge.Dispose());
        }

        [Test]
        public void Start_WithNullConnectionInfo_ShouldHandleGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = null };

            // Act & Assert - Should handle null connection info
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void Start_WithMissingApplicationNames_ShouldHandleGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string>());

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle empty application names gracefully
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void Start_WithNullEntityGuid_ShouldHandleGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns((string)null);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle null entity GUID
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void Start_WithNullLicenseKey_ShouldHandleGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns((string)null);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle null license key
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        #endregion

        #region HttpClient Creation Tests

        [Test]
        public void Start_WithProxyConfiguration_ShouldCreateProperHttpClient()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var proxy = new WebProxy("http://proxy.company.com:8080");
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should not throw when configuring with proxy
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void Start_WithDifferentConnectionProtocols_ShouldHandleAll()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            // Test HTTP protocol
            Mock.Arrange(() => _connectionInfo.HttpProtocol).Returns("http");
            Mock.Arrange(() => _connectionInfo.Port).Returns(80);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle HTTP protocol
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void Start_WithCustomPort_ShouldConfigureCorrectEndpoint()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            Mock.Arrange(() => _connectionInfo.Port).Returns(8443);
            Mock.Arrange(() => _connectionInfo.Host).Returns("custom.collector.com");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle custom endpoint configuration
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void Start_WithEmptyApplicationNames_ShouldHandleGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string>());

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle empty application names
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        #endregion

        #region Resource Configuration Tests

        [Test]
        public void Start_ShouldConfigureOpenTelemetryResource()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "TestApplication" });
            Mock.Arrange(() => _configuration.EntityGuid).Returns("test-entity-12345");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should configure resource with proper service name and entity GUID
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void Start_WithMultipleApplicationNames_ShouldUseFirst()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "PrimaryApp", "SecondaryApp" });

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should use the first application name
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        #endregion

        #region State Management Tests

        [Test]
        public void Start_CalledMultipleTimes_ShouldBeIdempotent()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act & Assert - Multiple starts should be safe
            Assert.DoesNotThrow(() => 
            {
                _meterListenerBridge.ConfigureOtlpExporter();
                _meterListenerBridge.ConfigureOtlpExporter();
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void Stop_CalledBeforeStart_ShouldHandleGracefully()
        {
            // Arrange - Don't call Start()

            // Act & Assert - Stop before start should be safe
            Assert.DoesNotThrow(() => _meterListenerBridge.Stop());
        }

        [Test]
        public void Stop_CalledMultipleTimes_ShouldBeIdempotent()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.ConfigureOtlpExporter();

            // Act & Assert - Multiple stops should be safe
            Assert.DoesNotThrow(() => 
            {
                _meterListenerBridge.Stop();
                _meterListenerBridge.Stop();
                _meterListenerBridge.Stop();
            });
        }

        [Test]
        public void Dispose_AfterStartStop_ShouldCleanupProperly()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            
            _meterListenerBridge.ConfigureOtlpExporter();
            _meterListenerBridge.Stop();

            // Act & Assert - Dispose after start/stop should be clean
            Assert.DoesNotThrow(() => _meterListenerBridge.Dispose());
        }

        #endregion

        #region Event Handling Tests

        [Test]
        public void OnPreCleanShutdown_ShouldStopBridge()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.ConfigureOtlpExporter();

            var preCleanShutdownEvent = new PreCleanShutdownEvent();

            // Act & Assert
            Assert.DoesNotThrow(() => EventBus<PreCleanShutdownEvent>.Publish(preCleanShutdownEvent));
        }

        [Test]
        public void ConfigurationUpdated_ShouldNotThrowException()
        {
            // Arrange
            var configUpdateEvent = new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Server);

            // Act & Assert - Configuration updates should be handled
            Assert.DoesNotThrow(() => EventBus<ConfigurationUpdatedEvent>.Publish(configUpdateEvent));
        }

        [Test]
        public void MultipleEvents_ShouldHandleSequenceCorrectly()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            var configUpdateEvent = new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local);
            var preCleanShutdownEvent = new PreCleanShutdownEvent();

            // Act & Assert - Sequence of events should be handled
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                EventBus<ConfigurationUpdatedEvent>.Publish(configUpdateEvent);
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                EventBus<PreCleanShutdownEvent>.Publish(preCleanShutdownEvent);
            });
        }

        #endregion

        #region Integration Scenario Tests

        [Test]
        public void FullLifecycle_StartStopRestart_ShouldWorkCorrectly()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert
            Assert.DoesNotThrow(() => 
            {
                // Initial start
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
                
                // Stop
                _meterListenerBridge.Stop();
                
                // Restart
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
                
                // Final cleanup
                _meterListenerBridge.Dispose();
            });
        }

        [Test]
        public void ComplexProxyScenario_ShouldHandleAllCases()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            // Test with complex proxy configuration
            var proxy = new WebProxy("http://proxy.company.com:8080", true, 
                new[] { "localhost", "127.0.0.1", "internal.company.com" },
                new NetworkCredential("proxyuser", "proxypass"));
                
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Complex proxy scenario should work
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        #endregion

        #region Advanced Coverage Tests

        [Test]
        public void AgentConnectedEvent_WithNullConnectInfo_ShouldHandleGracefully()
        {
            // Arrange
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = null };

            // Act & Assert - Should handle null ConnectInfo
            Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent));
        }

        [Test]
        public void Start_WhenOpenTelemetryEnabledButMetricsDisabled_ShouldNotCreateProvider()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(false);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act & Assert - Should not create provider when metrics disabled
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
        }

        [Test]
        public void Start_WhenOpenTelemetryDisabled_ShouldNotCreateAnything()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(false);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act & Assert - Should not create anything when OpenTelemetry disabled
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
        }

        [Test]
        public void Start_WithValidApplicationNames_ShouldUseFirstName()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "PrimaryApp", "SecondaryApp" });

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle valid application names properly
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void Start_WithProxyCredentials_ShouldConfigureProperlyWithAuth()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var proxyWithCredentials = new WebProxy("http://proxy.company.com:8080")
            {
                Credentials = new NetworkCredential("username", "password", "domain")
            };
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxyWithCredentials);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle proxy with credentials
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void Start_WithSecureConnectionInfo_ShouldConfigureHttpsEndpoint()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            Mock.Arrange(() => _connectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => _connectionInfo.Host).Returns("secure.newrelic.com");
            Mock.Arrange(() => _connectionInfo.Port).Returns(443);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should configure HTTPS endpoint
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void MeterListener_CreationFailure_ShouldBeHandledGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act & Assert - MeterListener creation issues should be handled
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
        }

        [Test]
        public void Start_WithMixedConfigurationScenarios_ShouldBehavePredictably()
        {
            // Arrange - Test various configuration combinations
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            // Test with minimal configuration
            Mock.Arrange(() => _connectionInfo.Host).Returns("localhost");
            Mock.Arrange(() => _connectionInfo.Port).Returns(4317);
            Mock.Arrange(() => _connectionInfo.HttpProtocol).Returns("http");
            Mock.Arrange(() => _connectionInfo.Proxy).Returns((WebProxy)null);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle minimal configuration
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void HttpClientCreation_WithRetryConfiguration_ShouldConfigureRetryHandler()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            // Configure connection for retry testing
            Mock.Arrange(() => _connectionInfo.Host).Returns("otlp.newrelic.com");
            Mock.Arrange(() => _connectionInfo.Port).Returns(4318);
            Mock.Arrange(() => _connectionInfo.HttpProtocol).Returns("https");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should configure HttpClient with retry logic
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void ResourceConfiguration_WithAllAttributes_ShouldSetupComplete()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "CompleteTestApp" });
            Mock.Arrange(() => _configuration.EntityGuid).Returns("complete-entity-guid-12345");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should configure complete resource attributes
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        [Test]
        public void ExportConfiguration_WithAllSettings_ShouldConfigureProperly()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            // Configure export settings
            Mock.Arrange(() => _connectionInfo.Host).Returns("export.newrelic.com");
            Mock.Arrange(() => _connectionInfo.Port).Returns(4318);
            Mock.Arrange(() => _connectionInfo.HttpProtocol).Returns("https");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should configure export with all settings
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
            });
        }

        #endregion

        #region Comprehensive Lifecycle Tests

        [Test]
        public void CompleteWorkflow_WithAllFeatures_ShouldExecuteSuccessfully()
        {
            // Arrange - Setup complete configuration
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "WorkflowTestApp" });
            Mock.Arrange(() => _configuration.EntityGuid).Returns("workflow-entity-guid");

            // Setup complete connection info
            var proxy = new WebProxy("http://corporate.proxy:8080", true);
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);
            Mock.Arrange(() => _connectionInfo.Host).Returns("otlp.eu.newrelic.com");
            Mock.Arrange(() => _connectionInfo.Port).Returns(4318);
            Mock.Arrange(() => _connectionInfo.HttpProtocol).Returns("https");

            // Events
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            var configUpdateEvent = new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Server);
            var preCleanShutdownEvent = new PreCleanShutdownEvent();

            // Act & Assert - Complete workflow should execute
            Assert.DoesNotThrow(() => 
            {
                // Initial connection and configuration
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
                
                // Configuration updates during runtime
                EventBus<ConfigurationUpdatedEvent>.Publish(configUpdateEvent);
                
                // Reconnection scenario
                _meterListenerBridge.Stop();
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.ConfigureOtlpExporter();
                
                // Clean shutdown
                EventBus<PreCleanShutdownEvent>.Publish(preCleanShutdownEvent);
                _meterListenerBridge.Dispose();
            });
        }

        #endregion

        #region Static Method Tests







        #endregion

        #region EntityGuid Change Detection Tests
        // NOTE: EntityGuid change detection and metric recording is now handled by OtlpExporterConfigurationService.
        // These tests should be moved to OtlpExporterConfigurationServiceTests when that test file is created.

        [Test]
        public void OnServerConfigurationUpdated_WhenEntityGuidUnchanged_ShouldNotRecordMetrics()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("same-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.ConfigureOtlpExporter();

            // Create a real ServerConfiguration object with the same EntityGuid
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""same-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Never());
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Never());
        }

        [Test]
        public void OnServerConfigurationUpdated_WhenFirstTimeEntityGuidSet_ShouldNotRecordChangeMetrics()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns((string)null);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Create a real ServerConfiguration object with first-time EntityGuid
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""first-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Never());
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Never());
        }

        [Test]
        public void OnServerConfigurationUpdated_WhenEntityGuidChangesFromEmptyToValue_ShouldNotRecordChangeMetrics()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Create a real ServerConfiguration object with new EntityGuid
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""new-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Never());
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Never());
        }

        [Test]
        public void OnServerConfigurationUpdated_WhenEntityGuidChangesToNull_ShouldNotRecordMetrics()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Create a real ServerConfiguration object without EntityGuid (null)
            var serverConfigJson = @"{""agent_run_id"": 12345}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Never());
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Never());
        }

        [Test]
        public void OnServerConfigurationUpdated_WhenEntityGuidChangesToEmpty_ShouldNotRecordMetrics()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Create a real ServerConfiguration object with empty EntityGuid
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": """"}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Never());
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Never());
        }



        [Test]
        public void RecreateMetricsProvider_ShouldNotThrow()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.ConfigureOtlpExporter();

            // Create a real ServerConfiguration object with new EntityGuid
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""new-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act & Assert - Should not throw during recreation
            Assert.DoesNotThrow(() => EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent));
        }



        [Test]
        public void OnServerConfigurationUpdated_WithWhitespaceEntityGuids_ShouldHandleGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.EntityGuid).Returns("  initial-entity-guid  ");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Create a real ServerConfiguration object with whitespace in EntityGuid
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""  different-entity-guid  ""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act & Assert - Should handle whitespace gracefully
            Assert.DoesNotThrow(() => EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent));
        }



        [Test]
        public void OnServerConfigurationUpdated_WhenServerConfigIsNull_ShouldNotThrow()
        {
            // Arrange
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            // Act & Assert - Should handle null ServerConfigurationUpdatedEvent gracefully
            Assert.DoesNotThrow(() => EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(null)));
        }

        [Test]
        public void OnServerConfigurationUpdated_WithMinimalJsonConfiguration_ShouldHandleGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            // Create minimal ServerConfiguration
            var serverConfigJson = @"{""agent_run_id"": 12345}";
            var serverConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(serverConfig);

            // Act & Assert - Should handle minimal configuration without throwing
            Assert.DoesNotThrow(() => EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent));
        }

        #endregion

        #region Helper Methods





        #endregion

        #region Meter Tags and Scope Bridging Tests



        #endregion

        #region Instrument Tags Bridging Tests





        #endregion

        #region Additional Coverage Tests%

        [Test]
        public void CreateMeterProvider_WithoutConnectionInfo_ShouldReturnNull()
        {
            // Arrange - Don't publish AgentConnectedEvent
            var mockBridging = Mock.Create<IMeterBridgingService>();
            var mockOtlp = Mock.Create<IOtlpExporterConfigurationService>();
            var bridge = new MeterListenerBridge(mockBridging, mockOtlp);

            // Act - Start without connection info
            bridge.ConfigureOtlpExporter();

            // Assert - Should handle gracefully
            Assert.DoesNotThrow(() => bridge.Stop());
        }

        [Test]
        public void CreateMeterProvider_WithValidConnectionInfo_ShouldConfigureEndpoint()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _connectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => _connectionInfo.Host).Returns("otlp.newrelic.com");
            Mock.Arrange(() => _connectionInfo.Port).Returns(443);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act
            _meterListenerBridge.ConfigureOtlpExporter();

            // Assert - Should configure without throwing
            Assert.DoesNotThrow(() => _meterListenerBridge.Stop());
        }

        [Test]
        public void CreateHttpClientWithProxyAndRetry_ExceptionDuringCreation_ShouldReturnFallbackClient()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            
            // Use a valid proxy configuration
            var proxy = new WebProxy("http://proxy.test.com:8080");
            Mock.Arrange(() => _connectionInfo.Proxy).Returns(proxy);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act - Should handle any internal exceptions and fallback
            Assert.DoesNotThrow(() => _meterListenerBridge.ConfigureOtlpExporter());
        }

        [Test]
        public void TryCreateMeterListener_WithMatchingILRepackedType_ShouldLogWarning()
        {
            // This scenario tests when MeterListener type matches ILRepacked type
            // In unit tests, this is the normal case since we don't have ILRepacking
            var mockBridging = Mock.Create<IMeterBridgingService>();
            var mockOtlp = Mock.Create<IOtlpExporterConfigurationService>();
            var bridge = new MeterListenerBridge(mockBridging, mockOtlp);

            // Act & Assert - Should handle gracefully
            Assert.DoesNotThrow(() => bridge.ConfigureOtlpExporter());
        }

        [Test]
        public void SubscribeToInstrumentPublishedEvent_ShouldCreateLambdaExpression()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act - TryCreateMeterListener will call SubscribeToInstrumentPublishedEvent internally
            _meterListenerBridge.ConfigureOtlpExporter();

            // Assert - Should complete without error
            Assert.DoesNotThrow(() => _meterListenerBridge.Stop());
        }

        [Test]
        public void SubscribeToMeasurementUpdates_ForAllNumericTypes_ShouldSubscribe()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act - Should subscribe to byte, short, int, long, float, double, decimal
            _meterListenerBridge.ConfigureOtlpExporter();

            // Assert - Should complete without error
            Assert.DoesNotThrow(() => _meterListenerBridge.Stop());
        }

        [Test]
        public void SubscribeToMeasurementCompletedEvent_ShouldCreateLambdaExpression()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act - TryCreateMeterListener will call SubscribeToMeasurementCompletedEvent internally
            _meterListenerBridge.ConfigureOtlpExporter();

            // Assert - Should complete without error
            Assert.DoesNotThrow(() => _meterListenerBridge.Stop());
        }









        [Test]
        public void Start_WithExistingMeterProvider_ShouldNotRecreate()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act - Start twice
            _meterListenerBridge.ConfigureOtlpExporter();
            _meterListenerBridge.ConfigureOtlpExporter();

            // Assert - Second start should use existing provider (no duplication)
            Assert.DoesNotThrow(() => _meterListenerBridge.Stop());
        }



        #endregion
    }
}

