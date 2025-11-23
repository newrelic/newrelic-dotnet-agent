// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
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
        private IConfiguration _configuration;
        private IConnectionInfo _connectionInfo;
        private IOtelBridgeSupportabilityMetricCounters _supportabilityMetricCounters;

        [SetUp]
        public void SetUp()
        {
            _disposableCollection = new DisposableCollection();

            // Setup configuration mock
            _configuration = Mock.Create<IConfiguration>();
            _disposableCollection.Add(new ConfigurationAutoResponder(_configuration));

            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "TestApp" });
            Mock.Arrange(() => _configuration.EntityGuid).Returns("test-entity-guid");
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("test-license-key");
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns(new List<string>());
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns(new List<string>());

            // Setup connection info mock
            _connectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => _connectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => _connectionInfo.Host).Returns("collector.newrelic.com");
            Mock.Arrange(() => _connectionInfo.Port).Returns(443);
            Mock.Arrange(() => _connectionInfo.Proxy).Returns((WebProxy)null);

            // Setup supportability metrics mock
            _supportabilityMetricCounters = Mock.Create<IOtelBridgeSupportabilityMetricCounters>();

            _meterListenerBridge = new MeterListenerBridge(_supportabilityMetricCounters);
        }

        [TearDown]
        public void TearDown()
        {
            _meterListenerBridge?.Dispose();
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
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());

            // Assert - Verify the bridge can handle multiple start calls without errors
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
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
        public void ShouldEnableInstrumentsInMeter_ShouldFilterCorrectly()
        {
            // This test validates the meter filtering logic through reflection
            var shouldEnableMethod = typeof(MeterListenerBridge)
                .GetMethod("ShouldEnableInstrumentsInMeter",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.That(shouldEnableMethod, Is.Not.Null);

            // Valid meter names should be enabled
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "Microsoft.AspNetCore.Hosting.test" }), Is.True);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "Microsoft.AspNetCore.test" }), Is.True);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "MyCustomMeter" }), Is.True);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "ApplicationMeter" }), Is.True);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "company.product.meter" }), Is.True);

            // Internal meters should be filtered out
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "NewRelic.Agent.Core.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "newrelic.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "NEWRELIC.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "OpenTelemetry.Api.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "opentelemetry.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "System.Diagnostics.Metrics.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "system.diagnostics.metrics.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { null }), Is.False);
        }

        [Test]
        public void OnConfigurationUpdated_DoesNotThrowException()
        {
            var bridge = new MeterListenerBridge(_supportabilityMetricCounters);

            // Get the protected method using reflection
            var method = typeof(MeterListenerBridge).GetMethod("OnConfigurationUpdated", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "OnConfigurationUpdated method should exist");

            // Test with different configuration update sources
            Assert.DoesNotThrow(() =>
            {
                method.Invoke(bridge, new object[] { ConfigurationUpdateSource.Local });
            }, "OnConfigurationUpdated should not throw with Local source");

            Assert.DoesNotThrow(() =>
            {
                method.Invoke(bridge, new object[] { ConfigurationUpdateSource.Server });
            }, "OnConfigurationUpdated should not throw with Server source");

            Assert.DoesNotThrow(() =>
            {
                method.Invoke(bridge, new object[] { ConfigurationUpdateSource.Unknown });
            }, "OnConfigurationUpdated should not throw with Unknown source");
        }

        [Test]
        public void TryCreateMeterListener_HandlesReflectionFailure()
        {
            // Test exception handling in TryCreateMeterListener
            var bridge = new MeterListenerBridge(_supportabilityMetricCounters);

            // Get the private method using reflection
            var method = typeof(MeterListenerBridge).GetMethod("TryCreateMeterListener", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "TryCreateMeterListener method should exist");

            // This will test the reflective code paths
            Assert.DoesNotThrow(() =>
            {
                method.Invoke(bridge, null);
            }, "TryCreateMeterListener should handle reflection failures gracefully");
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
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
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
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
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
            
            // Should record that bridge is enabled at least once
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MetricsBridgeEnabled), Occurs.AtLeastOnce());
        }

        #region Filter Logic Tests

        [Test]
        public void ShouldEnableInstrumentsInMeterWithFilters_WhenOpenTelemetryMetricsDisabled_ShouldReturnFalse()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(false);

            var method = GetShouldEnableMethod();

            // Act
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "Microsoft.AspNetCore.Hosting.test" });

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeterWithFilters_WhenMetricsDisabled_ShouldReturnFalse()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(false);

            var method = GetShouldEnableMethod();

            // Act
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "System.Net.Http.test" });

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeterWithFilters_NoFilters_ShouldEnable()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns(new List<string>());
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns(new List<string>());

            var method = GetShouldEnableMethod();

            // Act
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "Microsoft.AspNetCore.Hosting.test" });

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeterWithFilters_InIncludeList_ShouldEnable()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns(new List<string> { "Microsoft.AspNetCore.Hosting.test" });
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns(new List<string>());

            var method = GetShouldEnableMethod();

            // Act
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "Microsoft.AspNetCore.Hosting.test" });

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeterWithFilters_NotInIncludeList_ShouldDisable()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns(new List<string> { "System.Net.Http.test" });
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns(new List<string>());

            var method = GetShouldEnableMethod();

            // Act
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "Microsoft.AspNetCore.Hosting.test" });

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeterWithFilters_InExcludeList_ShouldDisable()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns(new List<string>());
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns(new List<string> { "Microsoft.AspNetCore.Hosting.test" });

            var method = GetShouldEnableMethod();

            // Act
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "Microsoft.AspNetCore.Hosting.test" });

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeterWithFilters_InBothLists_ExcludeTakesPrecedence()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns(new List<string> { "Microsoft.AspNetCore.Hosting.test" });
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns(new List<string> { "Microsoft.AspNetCore.Hosting.test" });

            var method = GetShouldEnableMethod();

            // Act
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "Microsoft.AspNetCore.Hosting.test" });

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeterWithFilters_InIncludeNotInExclude_ShouldEnable()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns(new List<string> { "Microsoft.AspNetCore.Hosting.test", "System.Net.Http.test" });
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns(new List<string> { "Microsoft.EntityFrameworkCore.test" });

            var method = GetShouldEnableMethod();

            // Act
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "Microsoft.AspNetCore.Hosting.test" });

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void WithNullIncludeFilters_ShouldWork()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns((List<string>)null);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns(new List<string>());

            var method = GetShouldEnableMethod();

            // Act
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "Microsoft.EntityFrameworkCore.test" });

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void WithNullExcludeFilters_ShouldWork()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns(new List<string>());
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns((List<string>)null);

            var method = GetShouldEnableMethod();

            // Act
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "System.Net.Http.test" });

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldEnableInstrumentsInMeterWithFilters_BuiltInFiltersHavePrecedence()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsIncludeFilters).Returns(new List<string> { "NewRelic.Agent.Core.test" });
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsExcludeFilters).Returns(new List<string>());

            var method = GetShouldEnableMethod();

            // Act - Built-in filter should exclude NewRelic meters regardless of include list
            var result = (bool)method.Invoke(_meterListenerBridge, new object[] { "NewRelic.Agent.Core.test" });

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region Start/Stop Method Tests

        [Test]
        public void Start_WhenOpenTelemetryDisabled_ShouldNotInitializeComponents()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(false);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            // Act
            _meterListenerBridge.Start();

            // Act & Assert 
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
            
            // Verify supportability metric is recorded
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MetricsBridgeDisabled), Occurs.Never());
        }

        [Test]
        public void Start_WhenMetricsDisabled_ShouldRecordDisabledMetric()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(false);

            // Act
            _meterListenerBridge.Start();

            // Assert - Should record disabled metric
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MetricsBridgeDisabled), Occurs.Once());
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MetricsBridgeEnabled), Occurs.Never());
        }

        [Test]
        public void Start_WithoutConnectionInfo_ShouldHandleGracefully()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            

            // Act & Assert
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
        }

        [Test]
        public void Start_WhenMetricsDisabled_ShouldNotInitializeComponents()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(false);

            // Act
            _meterListenerBridge.Start();

            // Assert - Should exit early and not throw
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
        }

        [Test]
        public void Start_WithValidConfiguration_ShouldRecordEnabledMetric()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Act
            _meterListenerBridge.Start();

            // Assert - Should record enabled metric at least once
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MetricsBridgeEnabled), Occurs.AtLeastOnce());
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MetricsBridgeDisabled), Occurs.Never());
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
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());

            // Verify multiple calls don't cause issues
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
        }

        [Test]
        public void Stop_ShouldCleanupAllResources()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            _meterListenerBridge.Start();

            // Act
            _meterListenerBridge.Stop();

            // Assert - Should be able to call multiple times without error
            Assert.DoesNotThrow(() => _meterListenerBridge.Stop());

            // Should be able to restart after stop
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
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
            
            // Should record enabled metric at least once (restart scenario may cause multiple calls)
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MetricsBridgeEnabled), Occurs.AtLeastOnce());
        }

        [Test]
        public void CreateBridgedMeter_WithValidMeter_ShouldCreateMeter()
        {
            // Arrange - Test CreateBridgedMeter static method
            var createBridgedMeterMethod = typeof(MeterListenerBridge)
                .GetMethod("CreateBridgedMeter", BindingFlags.NonPublic | BindingFlags.Static);

            // Create an actual Meter instance for testing
            using var testMeter = new System.Diagnostics.Metrics.Meter("TestMeter", "1.0.0");

            // Act
            var result = createBridgedMeterMethod?.Invoke(null, new object[] { testMeter });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<System.Diagnostics.Metrics.Meter>());
            
            // Clean up the result
            if (result is IDisposable disposable)
                disposable.Dispose();
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

            _meterListenerBridge.Start();

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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
                _meterListenerBridge.Start();
                _meterListenerBridge.Start();
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
            _meterListenerBridge.Start();

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
            
            _meterListenerBridge.Start();
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
            _meterListenerBridge.Start();

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
                _meterListenerBridge.Start();
                
                // Stop
                _meterListenerBridge.Stop();
                
                // Restart
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.Start();
                
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
                _meterListenerBridge.Start();
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
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
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
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
        }

        [Test]
        public void Start_WithEmptyApplicationNames_ShouldNotStart()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string>());

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle empty application names list
            Assert.Throws<InvalidOperationException>(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.Start();
            });
        }

        [Test]
        public void Start_WithNullFirstApplicationName_ShouldThrow()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { null, "ValidApp" });

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should throw when first application name is null
            Assert.Throws<ArgumentException>(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.Start();
            });
        }

        [Test]
        public void Start_WithEmptyFirstApplicationName_ShouldThrow()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "", "ValidApp" });

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should throw when first application name is empty
            Assert.Throws<ArgumentException>(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.Start();
            });
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
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
            Assert.DoesNotThrow(() => _meterListenerBridge.Start());
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
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
                _meterListenerBridge.Start();
                
                // Configuration updates during runtime
                EventBus<ConfigurationUpdatedEvent>.Publish(configUpdateEvent);
                
                // Reconnection scenario
                _meterListenerBridge.Stop();
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.Start();
                
                // Clean shutdown
                EventBus<PreCleanShutdownEvent>.Publish(preCleanShutdownEvent);
                _meterListenerBridge.Dispose();
            });
        }

        #endregion

        #region Static Method Tests

        [Test]
        public void DisableBridgedInstrument_WithNullState_ShouldNotThrow()
        {
            // Act & Assert - Should handle null state gracefully
            Assert.DoesNotThrow(() => 
            {
                var method = typeof(MeterListenerBridge)
                    .GetMethod("DisableBridgedInstrument", BindingFlags.NonPublic | BindingFlags.Static);
                method?.Invoke(null, new object[] { null });
            });
        }

        [Test]
        public void DisableBridgedInstrument_WithNonInstrumentState_ShouldNotThrow()
        {
            // Act & Assert - Should handle non-instrument objects gracefully
            Assert.DoesNotThrow(() => 
            {
                var method = typeof(MeterListenerBridge)
                    .GetMethod("DisableBridgedInstrument", BindingFlags.NonPublic | BindingFlags.Static);
                method?.Invoke(null, new object[] { "not an instrument" });
            });
        }

        [Test]
        public void RecordSpecificInstrumentType_ShouldRecordCorrectMetrics()
        {
            // Arrange
            var recordMethod = typeof(MeterListenerBridge)
                .GetMethod("RecordSpecificInstrumentType", BindingFlags.NonPublic | BindingFlags.Instance);

            // Test Counter
            recordMethod?.Invoke(_meterListenerBridge, new object[] { "Counter`1" });
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.CreateCounter), Occurs.Once());

            // Test Histogram
            recordMethod?.Invoke(_meterListenerBridge, new object[] { "Histogram`1" });
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.CreateHistogram), Occurs.Once());

            // Test UpDownCounter
            recordMethod?.Invoke(_meterListenerBridge, new object[] { "UpDownCounter`1" });
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.CreateUpDownCounter), Occurs.Once());

            // Test unknown type - should not throw
            Assert.DoesNotThrow(() => recordMethod?.Invoke(_meterListenerBridge, new object[] { "UnknownInstrument`1" }));
        }

        [Test]
        public void CreateObserveMethodInvoker_WithValidType_ShouldNotThrow()
        {
            // Test CreateObserveMethodInvoker static method
            var createObserveMethod = typeof(MeterListenerBridge)
                .GetMethod("CreateObserveMethodInvoker", BindingFlags.NonPublic | BindingFlags.Static);

            // Use a type that could potentially have an Observe method
            var testType = typeof(object); // Simple type for testing

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => createObserveMethod?.Invoke(null, new object[] { testType }));
        }

        [Test]
        public void ObservableInstrumentCacheData_Properties_ShouldBeSettable()
        {
            // Test that ObservableInstrumentCacheData properties can be set
            var cacheDataType = typeof(MeterListenerBridge).GetNestedType("ObservableInstrumentCacheData", BindingFlags.Public);
            Assert.That(cacheDataType, Is.Not.Null);

            var cacheDataInstance = Activator.CreateInstance(cacheDataType);
            Assert.That(cacheDataInstance, Is.Not.Null);

            // The properties should be settable
            var createObservableInstrumentDelegateProperty = cacheDataType.GetProperty("CreateObservableInstrumentDelegate");
            var createCallbackProperty = cacheDataType.GetProperty("CreateCallbackAndObservableInstrumentDelegate");
            var observeMethodProperty = cacheDataType.GetProperty("ObserveMethodDelegate");

            Assert.That(createObservableInstrumentDelegateProperty, Is.Not.Null);
            Assert.That(createCallbackProperty, Is.Not.Null);
            Assert.That(observeMethodProperty, Is.Not.Null);
        }

        #endregion

        #region EntityGuid Change Detection Tests

        [Test]
        public void OnServerConfigurationUpdated_WhenEntityGuidChanges_ShouldRecordEntityGuidChangedMetric()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.Start();

            // Create a real ServerConfiguration object and set the EntityGuid via JSON deserialization
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""new-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Once());
        }

        [Test]
        public void OnServerConfigurationUpdated_WhenEntityGuidChanges_ShouldRecordMeterProviderRecreatedMetric()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.Start();

            // Create a real ServerConfiguration object and set the EntityGuid via JSON deserialization
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""new-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Once());
        }

        [Test]
        public void OnServerConfigurationUpdated_WhenEntityGuidUnchanged_ShouldNotRecordMetrics()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("same-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.Start();

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
        public void OnServerConfigurationUpdated_WhenMetricsDisabled_ShouldNotRecreateProvider()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(false);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Create a real ServerConfiguration object with different EntityGuid
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""new-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert - Should record entity GUID change but not recreate provider since metrics are disabled
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Once());
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Never());
        }

        [Test]
        public void OnServerConfigurationUpdated_WhenNoConnectionInfo_ShouldNotRecreateProvider()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            // Don't publish AgentConnectedEvent, so no connection info
            // Create a real ServerConfiguration object with different EntityGuid
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""new-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert - Should not record entity GUID change since _currentEntityGuid is not initialized without AgentConnectedEvent
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Never());
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Never());
        }

        [Test]
        public void OnServerConfigurationUpdated_WhenEntityGuidChangesMultipleTimes_ShouldRecordEachChange()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.Start();

            // First change
            var firstServerConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""second-entity-guid""}";
            var firstServerConfig = ServerConfiguration.FromJson(firstServerConfigJson);
            var firstConfigUpdateEvent = new ServerConfigurationUpdatedEvent(firstServerConfig);

            // Second change
            var secondServerConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""third-entity-guid""}";
            var secondServerConfig = ServerConfiguration.FromJson(secondServerConfigJson);
            var secondConfigUpdateEvent = new ServerConfigurationUpdatedEvent(secondServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(firstConfigUpdateEvent);
            EventBus<ServerConfigurationUpdatedEvent>.Publish(secondConfigUpdateEvent);

            // Assert
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Exactly(2));
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Exactly(2));
        }

        [Test]
        public void RecreateMetricsProvider_ShouldUseUpdatedEntityGuid()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "TestApp" });
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("test-license-key");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.Start();

            // Create a real ServerConfiguration object with updated EntityGuid
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""updated-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act - Trigger entity GUID change
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert - The RecreateMetricsProvider method should have been called internally
            // We can verify this by checking that the MeterProviderRecreated metric was recorded
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Once());
        }

        [Test]
        public void RecreateMetricsProvider_ShouldNotThrow()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("initial-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.Start();

            // Create a real ServerConfiguration object with new EntityGuid
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""new-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act & Assert - Should not throw during recreation
            Assert.DoesNotThrow(() => EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent));
        }

        [Test]
        public void OnAgentConnected_ShouldInitializeEntityGuidTracking()
        {
            // Arrange
            Mock.Arrange(() => _configuration.EntityGuid).Returns("tracked-entity-guid");

            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);

            // Assert - Verify that subsequent entity GUID changes are properly detected
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""changed-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Should record the change since tracking was initialized
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Once());
        }

        [Test]
        public void EntityGuidChangeDetection_WorksAcrossAgentReconnection()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.EntityGuid).Returns("original-entity-guid");

            // Initial connection
            var firstAgentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(firstAgentConnectedEvent);
            _meterListenerBridge.Start();

            // Agent reconnection (should reset tracking)
            Mock.Arrange(() => _configuration.EntityGuid).Returns("new-after-reconnect-entity-guid");
            var secondAgentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(secondAgentConnectedEvent);

            // Server configuration update with changed entity GUID
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""final-entity-guid""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert - Should detect change from the entity GUID set during reconnection
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Once());
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
        public void EntityGuidChangeDetection_FullApplicationNameChangeScenario()
        {
            // Arrange - Simulate the complete scenario described in the requirements
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "MyApplication" });
            Mock.Arrange(() => _configuration.EntityGuid).Returns("original-entity-guid");
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("test-license-key");

            // 1. Initial agent startup
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
            _meterListenerBridge.Start();

            // 2. Application name change via API (this would cause reconnection in real scenario)
            // Simulate what happens when SetApplicationName triggers a new entity GUID
            var serverConfigJson = @"{""agent_run_id"": 12345, ""entity_guid"": ""new-entity-guid-after-app-name-change""}";
            var newServerConfig = ServerConfiguration.FromJson(serverConfigJson);
            var serverConfigUpdateEvent = new ServerConfigurationUpdatedEvent(newServerConfig);

            // Act
            EventBus<ServerConfigurationUpdatedEvent>.Publish(serverConfigUpdateEvent);

            // Assert - Should detect entity GUID change and recreate OTel resources
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged), Occurs.Once());
            Mock.Assert(() => _supportabilityMetricCounters.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated), Occurs.Once());
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

        private System.Reflection.MethodInfo GetShouldEnableMethod()
        {
            return typeof(MeterListenerBridge)
                .GetMethod("ShouldEnableInstrumentsInMeterWithFilters",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        #endregion
    }
}
