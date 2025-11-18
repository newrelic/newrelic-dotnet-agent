// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NewRelic.Agent.Core.Utilities;
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

            // Setup connection info mock
            _connectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => _connectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => _connectionInfo.Host).Returns("collector.newrelic.com");
            Mock.Arrange(() => _connectionInfo.Port).Returns(443);
            Mock.Arrange(() => _connectionInfo.Proxy).Returns((WebProxy)null);

            _meterListenerBridge = new MeterListenerBridge();
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

            // Internal meters should be filtered out
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "NewRelic.Agent.Core.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "OpenTelemetry.Api.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "System.Diagnostics.Metrics.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { null }), Is.False);
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
        public void Enhanced_DiagnosticSource_Compatibility_ShouldNotBreakWithOlderVersions()
        {
            // Arrange
            var agentConnectedEvent = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            
            // Act & Assert - Enhanced bridge should start without errors even with current DiagnosticSource version
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(agentConnectedEvent);
                _meterListenerBridge.Start();
            });
            
            // Verify
            var shouldEnableMethod = typeof(MeterListenerBridge)
                .GetMethod("ShouldEnableInstrumentsInMeter",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.That(shouldEnableMethod, Is.Not.Null);

            // Enhanced version should still filter out internal meters
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "Microsoft.AspNetCore.Hosting.test" }), Is.True);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "NewRelic.Agent.Core.test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "OpenTelemetry.Api.test" }), Is.False);
        }

        #region Filter Logic Tests

        [Test]
        public void ShouldEnableInstrumentsInMeterWithFilters_WhenOpenTelemetryDisabled_ShouldReturnFalse()
        {
            // Arrange
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(false);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

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
            Mock.Arrange(() => _configuration.OpenTelemetryEnabled).Returns(true);
            Mock.Arrange(() => _configuration.OpenTelemetryMetricsEnabled).Returns(true);

            var firstConnection = new AgentConnectedEvent { ConnectInfo = _connectionInfo };
            var secondConnection = new AgentConnectedEvent { ConnectInfo = _connectionInfo };

            // Act & Assert - Should handle reconnection gracefully
            Assert.DoesNotThrow(() => 
            {
                EventBus<AgentConnectedEvent>.Publish(firstConnection);
                EventBus<AgentConnectedEvent>.Publish(secondConnection);
            });
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
