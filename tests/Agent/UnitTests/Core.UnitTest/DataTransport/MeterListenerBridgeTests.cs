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
using NewRelic.Agent.Core.Samplers;
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
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "MyApp.Metrics" }), Is.True);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "Microsoft.AspNetCore" }), Is.True);

            // Internal meters should be filtered out
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "NewRelic.Test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "OpenTelemetry.Test" }), Is.False);
            Assert.That((bool)shouldEnableMethod.Invoke(null, new object[] { "System.Diagnostics.Metrics" }), Is.False);
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
        public void CreateBridgedInstrumentDelegate_ShouldSupportGaugeAtRuntime()
        {
            // Arrange: Dynamically create a Gauge<int> instrument type (simulate .NET 8+)
            var meter = new System.Diagnostics.Metrics.Meter("TestGaugeMeter");
            Type gaugeType = null;
            object gaugeInstance = null;
            try
            {
                // Try to get the Gauge<T> type via reflection (only exists in .NET 8+)
                var metricsAssembly = typeof(System.Diagnostics.Metrics.Meter).Assembly;
                gaugeType = metricsAssembly.GetType("System.Diagnostics.Metrics.Gauge`1");
                if (gaugeType == null)
                {
                    Assert.Ignore("Gauge<T> is not available in this runtime. Test ignored.");
                }

                // Try to find both 4-parameter and 3-parameter CreateGauge<T> overloads
                var createGaugeMethods = typeof(System.Diagnostics.Metrics.Meter).GetMethods()
                    .Where(m => m.Name == "CreateGauge" && m.IsGenericMethod).ToList();

                // Prefer 4-parameter overload (name, unit, description, tags)
                var createGauge4 = createGaugeMethods.FirstOrDefault(m =>
                    m.GetParameters().Select(p => p.ParameterType)
                        .SequenceEqual(new[] { typeof(string), typeof(string), typeof(string), typeof(IEnumerable<KeyValuePair<string, object>>) })
                );
                var createGauge3 = createGaugeMethods.FirstOrDefault(m =>
                    m.GetParameters().Select(p => p.ParameterType)
                        .SequenceEqual(new[] { typeof(string), typeof(string), typeof(string) })
                );

                if (createGauge4 != null)
                {
                    var genericCreateGauge4 = createGauge4.MakeGenericMethod(typeof(int));
                    gaugeInstance = genericCreateGauge4.Invoke(meter, new object[] { "testGauge", null, null, null });
                }
                else if (createGauge3 != null)
                {
                    var genericCreateGauge3 = createGauge3.MakeGenericMethod(typeof(int));
                    gaugeInstance = genericCreateGauge3.Invoke(meter, new object[] { "testGauge", null, null });
                }
                else
                {
                    Assert.Ignore("Meter.CreateGauge<T> is not available in this runtime. Test ignored.");
                }
            }
            catch (Exception ex)
            {
                Assert.Ignore($"Gauge<T> or Meter.CreateGauge<T> not available: {ex.Message}");
            }

            // Act: Use the agent's reflection-based delegate creation
            var createDelegate = (Func<System.Diagnostics.Metrics.Meter, string, string, string, object>)
                typeof(MeterListenerBridge)
                    .GetMethod("CreateBridgedInstrumentDelegate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { gaugeInstance.GetType() });

            // Assert
            Assert.That(createDelegate, Is.Not.Null, "Delegate for Gauge<T> should be created at runtime");
            var createdGauge = createDelegate(meter, "runtimeGauge", null, null);
            Assert.That(createdGauge, Is.Not.Null, "Gauge<T> instrument should be created at runtime");
            Assert.That(createdGauge.GetType().Name.StartsWith("Gauge`1"), "Created instrument should be a Gauge<T>");
        }
    }

    namespace NewRelic.Agent.Core.DataTransport
    {
        [TestFixture]
        public class CustomRetryHandlerTests
        {
            private TestMessageHandler _innerHandler;
            private CustomRetryHandler _retryHandler;
            private HttpClient _httpClient;

            [SetUp]
            public void SetUp()
            {
                _innerHandler = new TestMessageHandler();
                _retryHandler = new CustomRetryHandler
                {
                    InnerHandler = _innerHandler
                };
                _httpClient = new HttpClient(_retryHandler);
            }

            [TearDown]
            public void TearDown()
            {
                _httpClient?.Dispose();
                _retryHandler?.Dispose();
                _innerHandler?.Dispose();
            }

            [Test]
            public async Task SendAsync_SuccessfulRequest_ShouldNotRetry()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                _innerHandler.SetResponse(HttpStatusCode.OK, "Success");

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry on success");
            }

            [Test]
            public async Task SendAsync_TransientFailure_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponses(
                    new TestResponse { StatusCode = HttpStatusCode.ServiceUnavailable, Content = "Temp failure" },
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success" }
                );

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry once on transient failure");
            }

            [Test]
            public async Task SendAsync_PermanentFailure_ShouldNotRetry()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                _innerHandler.SetResponse(HttpStatusCode.BadRequest, "Bad Request");

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry on permanent failure");
            }

            [Test]
            public async Task SendAsync_MaxRetriesExhausted_ShouldReturnFailure()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponse(HttpStatusCode.ServiceUnavailable, "Always fails");

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(3), "Should retry up to max attempts");
            }

            [Test]
            public async Task SendAsync_NetworkException_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                _innerHandler.SetExceptionThenSuccess(new HttpRequestException("Network error"));

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry after network exception");
            }

            [Test]
            public async Task SendAsync_TaskCancelledException_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                _innerHandler.SetExceptionThenSuccess(new TaskCanceledException("Timeout"));

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry after timeout");
            }

            [Test]
            public async Task SendAsync_CancellationRequested_ShouldRespectCancellation()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                var cancellationToken = new CancellationToken(true); // Already cancelled
                _innerHandler.SetResponse(HttpStatusCode.OK, "Success");

                // Act & Assert
                try
                {
                    await _httpClient.SendAsync(request, cancellationToken);
                    Assert.Fail("Expected TaskCanceledException but none was thrown");
                }
                catch (TaskCanceledException)
                {
                    // Expected - test passes
                    Assert.Pass();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected TaskCanceledException but got {ex.GetType().Name}: {ex.Message}");
                }
            }

            [Test]
            public async Task SendAsync_NonRetryableException_ShouldThrowImmediately()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                _innerHandler.SetException(new ArgumentException("Non-retryable"));

                // Act & Assert
                try
                {
                    await _httpClient.SendAsync(request, CancellationToken.None);
                    Assert.Fail("Expected ArgumentException but none was thrown");
                }
                catch (ArgumentException ex)
                {
                    Assert.That(ex.Message, Is.EqualTo("Non-retryable"));
                    Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry non-retryable exceptions");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected ArgumentException but got {ex.GetType().Name}: {ex.Message}");
                }
            }

            #region HTTP Status Code Retry Edge Cases

            [Test]
            public async Task SendAsync_HttpStatus500_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponses(
                    new TestResponse { StatusCode = HttpStatusCode.InternalServerError, Content = "Server Error" },
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success" }
                );

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry on 500 Internal Server Error");
            }

            [Test]
            public async Task SendAsync_HttpStatus502_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponses(
                    new TestResponse { StatusCode = HttpStatusCode.BadGateway, Content = "Bad Gateway" },
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success" }
                );

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry on 502 Bad Gateway");
            }

            [Test]
            public async Task SendAsync_HttpStatus503_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponses(
                    new TestResponse { StatusCode = HttpStatusCode.ServiceUnavailable, Content = "Service Unavailable" },
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success" }
                );

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry on 503 Service Unavailable");
            }

            [Test]
            public async Task SendAsync_HttpStatus504_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponses(
                    new TestResponse { StatusCode = HttpStatusCode.GatewayTimeout, Content = "Gateway Timeout" },
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success" }
                );

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry on 504 Gateway Timeout");
            }

            [Test]
            public async Task SendAsync_HttpStatus429_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponses(
                    new TestResponse { StatusCode = (HttpStatusCode)429, Content = "Too Many Requests" }, // 429 as numeric value
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success" }
                );

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry on 429 Too Many Requests");
            }

            [Test]
            public async Task SendAsync_HttpStatus401_ShouldNotRetry()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponse(HttpStatusCode.Unauthorized, "Unauthorized");

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry on 401 Unauthorized");
            }

            [Test]
            public async Task SendAsync_HttpStatus403_ShouldNotRetry()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponse(HttpStatusCode.Forbidden, "Forbidden");

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry on 403 Forbidden");
            }

            [Test]
            public async Task SendAsync_HttpStatus404_ShouldNotRetry()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                _innerHandler.SetResponse(HttpStatusCode.NotFound, "Not Found");

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry on 404 Not Found");
            }

            [Test]
            public async Task SendAsync_HttpStatus422_ShouldNotRetry()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponse((HttpStatusCode)422, "Unprocessable Entity"); // 422 as numeric value

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo((HttpStatusCode)422));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry on 422 Unprocessable Entity");
            }

            [Test]
            public async Task SendAsync_MixedRetryableAndNonRetryableErrors()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetResponses(
                    new TestResponse { StatusCode = HttpStatusCode.ServiceUnavailable, Content = "Service Unavailable" },
                    new TestResponse { StatusCode = HttpStatusCode.BadGateway, Content = "Bad Gateway" },
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success" }
                );

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(3), "Should retry through multiple transient errors");
            }

            #endregion

            #region Exception Retry Edge Cases

            [Test]
            public async Task SendAsync_SocketException_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                // Use HttpRequestException wrapping SocketException - more realistic scenario
                var socketException = new System.Net.Sockets.SocketException(10054); // Connection reset by peer
                var httpException = new HttpRequestException("Network error", socketException);
                _innerHandler.SetExceptionThenSuccess(httpException);

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry after SocketException wrapped in HttpRequestException");
            }

            [Test]
            public async Task SendAsync_TimeoutException_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                // Use HttpRequestException wrapping TimeoutException - more realistic scenario
                var timeoutException = new TimeoutException("Request timeout");
                var httpException = new HttpRequestException("Request timed out", timeoutException);
                _innerHandler.SetExceptionThenSuccess(httpException);

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry after TimeoutException wrapped in HttpRequestException");
            }

            [Test]
            public async Task SendAsync_TaskCanceledExceptionWithTimeout_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                // Create TaskCanceledException that simulates timeout (not cancellation)
                var timeoutException = new TaskCanceledException("A task was canceled.", new TimeoutException());
                _innerHandler.SetExceptionThenSuccess(timeoutException);

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry timeout-based TaskCanceledException");
            }

            [Test]
            public async Task SendAsync_WebException_ShouldRetryAndSucceed()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                // Use HttpRequestException wrapping WebException - more realistic scenario
                var webException = new WebException("Connection failed", WebExceptionStatus.ConnectFailure);
                var httpException = new HttpRequestException("Connection failed", webException);
                _innerHandler.SetExceptionThenSuccess(httpException);

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(2), "Should retry after WebException wrapped in HttpRequestException");
            }

            [Test]
            public async Task SendAsync_InvalidOperationException_ShouldNotRetry()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetException(new InvalidOperationException("Invalid operation"));

                // Act & Assert
                try
                {
                    await _httpClient.SendAsync(request, CancellationToken.None);
                    Assert.Fail("Expected InvalidOperationException but none was thrown");
                }
                catch (InvalidOperationException ex)
                {
                    Assert.That(ex.Message, Is.EqualTo("Invalid operation"));
                    Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry InvalidOperationException");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected InvalidOperationException but got {ex.GetType().Name}: {ex.Message}");
                }
            }

            [Test]
            public async Task SendAsync_ObjectDisposedException_ShouldNotRetry()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                _innerHandler.SetException(new ObjectDisposedException("HttpClient"));

                // Act & Assert
                try
                {
                    await _httpClient.SendAsync(request, CancellationToken.None);
                    Assert.Fail("Expected ObjectDisposedException but none was thrown");
                }
                catch (ObjectDisposedException)
                {
                    Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry ObjectDisposedException");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected ObjectDisposedException but got {ex.GetType().Name}: {ex.Message}");
                }
            }

            [Test]
            public async Task SendAsync_OutOfMemoryException_ShouldNotRetry()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetException(new OutOfMemoryException("Out of memory"));

                // Act & Assert
                try
                {
                    await _httpClient.SendAsync(request, CancellationToken.None);
                    Assert.Fail("Expected OutOfMemoryException but none was thrown");
                }
                catch (OutOfMemoryException)
                {
                    Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry OutOfMemoryException");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected OutOfMemoryException but got {ex.GetType().Name}: {ex.Message}");
                }
            }

            [Test]
            public async Task SendAsync_DirectSocketException_ShouldNotRetryUnlessWrapped()
            {
                // Arrange - Test that direct SocketException (not wrapped in HttpRequestException) is not retried
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                _innerHandler.SetException(new System.Net.Sockets.SocketException(10054)); // Connection reset by peer

                // Act & Assert
                try
                {
                    await _httpClient.SendAsync(request, CancellationToken.None);
                    Assert.Fail("Expected SocketException but none was thrown");
                }
                catch (System.Net.Sockets.SocketException)
                {
                    Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry direct SocketException (only when wrapped in HttpRequestException)");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected SocketException but got {ex.GetType().Name}: {ex.Message}");
                }
            }

            [Test]
            public async Task SendAsync_DirectTimeoutException_ShouldNotRetryUnlessWrapped()
            {
                // Arrange - Test that direct TimeoutException (not wrapped in HttpRequestException) is not retried
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                _innerHandler.SetException(new TimeoutException("Request timeout"));

                // Act & Assert
                try
                {
                    await _httpClient.SendAsync(request, CancellationToken.None);
                    Assert.Fail("Expected TimeoutException but none was thrown");
                }
                catch (TimeoutException)
                {
                    Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry direct TimeoutException (only when wrapped in HttpRequestException)");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected TimeoutException but got {ex.GetType().Name}: {ex.Message}");
                }
            }

            [Test]
            public async Task SendAsync_DirectWebException_ShouldNotRetryUnlessWrapped()
            {
                // Arrange - Test that direct WebException (not wrapped in HttpRequestException) is not retried
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                var webException = new WebException("Connection failed", WebExceptionStatus.ConnectFailure);
                _innerHandler.SetException(webException);

                // Act & Assert
                try
                {
                    await _httpClient.SendAsync(request, CancellationToken.None);
                    Assert.Fail("Expected WebException but none was thrown");
                }
                catch (WebException)
                {
                    Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should not retry direct WebException (only when wrapped in HttpRequestException)");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected WebException but got {ex.GetType().Name}: {ex.Message}");
                }
            }

            #endregion

            #region Retry Timing and Behavior Edge Cases

            [Test]
            public async Task SendAsync_CancellationDuringRetryDelay_ShouldRespectCancellation()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                var cts = new CancellationTokenSource();

                // First request fails, second should be cancelled during retry delay
                _innerHandler.SetResponses(
                    new TestResponse { StatusCode = HttpStatusCode.ServiceUnavailable, Content = "Temp failure" },
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success" }
                );

                // Start the request
                var requestTask = _httpClient.SendAsync(request, cts.Token);

                // Cancel after a short delay (should catch it during retry delay)
                cts.CancelAfter(50);

                // Act & Assert
                try
                {
                    await requestTask;
                    Assert.Fail("Expected TaskCanceledException but none was thrown");
                }
                catch (TaskCanceledException)
                {
                    // Expected - test passes
                    Assert.That(_innerHandler.RequestCount, Is.EqualTo(1), "Should have made one attempt before cancellation");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected TaskCanceledException but got {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    cts.Dispose();
                }
            }

            [Test]
            public async Task SendAsync_ExactlyMaxRetries_ShouldStopAtLimit()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                // Set up exactly MaxRetries (3) responses, all failures
                _innerHandler.SetResponses(
                    new TestResponse { StatusCode = HttpStatusCode.ServiceUnavailable, Content = "Failure 1" },
                    new TestResponse { StatusCode = HttpStatusCode.ServiceUnavailable, Content = "Failure 2" },
                    new TestResponse { StatusCode = HttpStatusCode.ServiceUnavailable, Content = "Failure 3" },
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Should not reach this" }
                );

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(3), "Should stop exactly at MaxRetries limit");
            }

            [Test]
            public async Task SendAsync_MultipleSequentialRequests_ShouldResetRetryCount()
            {
                // Arrange
                var request1 = new HttpRequestMessage(HttpMethod.Post, "https://test1.example.com");
                var request2 = new HttpRequestMessage(HttpMethod.Get, "https://test2.example.com");

                // First request: fail twice then succeed
                _innerHandler.SetResponses(
                    new TestResponse { StatusCode = HttpStatusCode.ServiceUnavailable, Content = "Fail" },
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success 1" },
                    // Second request responses
                    new TestResponse { StatusCode = HttpStatusCode.ServiceUnavailable, Content = "Fail" },
                    new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success 2" }
                );

                // Act
                var response1 = await _httpClient.SendAsync(request1, CancellationToken.None);
                var requestCountAfterFirst = _innerHandler.RequestCount;

                var response2 = await _httpClient.SendAsync(request2, CancellationToken.None);
                var requestCountAfterSecond = _innerHandler.RequestCount;

                // Assert
                Assert.That(response1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(requestCountAfterFirst, Is.EqualTo(2), "First request should make 2 attempts");

                Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(requestCountAfterSecond, Is.EqualTo(4), "Second request should make 2 more attempts");
            }

            [Test]
            public async Task SendAsync_EmptyResponseContent_ShouldHandleGracefully()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                _innerHandler.SetResponse(HttpStatusCode.OK, ""); // Empty content

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);
                var content = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(content, Is.EqualTo(""));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
            }

            [Test]
            public async Task SendAsync_LargeResponseContent_ShouldHandleCorrectly()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Post, "https://test.example.com");
                var largeContent = new string('X', 10000); // 10KB of content
                _innerHandler.SetResponse(HttpStatusCode.OK, largeContent);

                // Act
                var response = await _httpClient.SendAsync(request, CancellationToken.None);
                var content = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(content, Is.EqualTo(largeContent));
                Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
            }

            [Test]
            public async Task SendAsync_DisposedHttpClient_ShouldHandleGracefully()
            {
                // Arrange
                var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com");
                _innerHandler.SetResponse(HttpStatusCode.OK, "Success");

                // Dispose the client
                _httpClient.Dispose();

                // Act & Assert
                try
                {
                    await _httpClient.SendAsync(request, CancellationToken.None);
                    Assert.Fail("Expected ObjectDisposedException but none was thrown");
                }
                catch (ObjectDisposedException)
                {
                    // Expected - client is disposed
                    Assert.Pass();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Expected ObjectDisposedException but got {ex.GetType().Name}: {ex.Message}");
                }
            }

            #endregion

            // Test helper class to simulate HTTP responses and exceptions
            private class TestMessageHandler : HttpMessageHandler
            {
                private readonly Queue<TestResponse> _responses = new Queue<TestResponse>();
                private readonly Queue<Exception> _exceptions = new Queue<Exception>();
                private TestResponse _lastResponse = null;
                private int _requestCount = 0;

                public int RequestCount => _requestCount;

                public void SetResponse(HttpStatusCode statusCode, string content)
                {
                    var response = new TestResponse { StatusCode = statusCode, Content = content };
                    _responses.Enqueue(response);
                    _lastResponse = response; // Keep track of the last response for repeating
                }

                public void SetResponses(params TestResponse[] responses)
                {
                    foreach (var response in responses)
                    {
                        _responses.Enqueue(response);
                    }
                    if (responses.Length > 0)
                    {
                        _lastResponse = responses[responses.Length - 1]; // Keep the last response
                    }
                }

                public void SetException(Exception exception)
                {
                    _exceptions.Enqueue(exception);
                }

                public void SetExceptionThenSuccess(Exception exception)
                {
                    _exceptions.Enqueue(exception);
                    _responses.Enqueue(new TestResponse { StatusCode = HttpStatusCode.OK, Content = "Success" });
                }

                protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    _requestCount++;
                    cancellationToken.ThrowIfCancellationRequested();

                    // Simulate delay for testing
                    await Task.Delay(1, cancellationToken);

                    if (_exceptions.Count > 0)
                    {
                        throw _exceptions.Dequeue();
                    }

                    if (_responses.Count > 0)
                    {
                        var testResponse = _responses.Dequeue();
                        _lastResponse = testResponse; // Update last response
                        return new HttpResponseMessage(testResponse.StatusCode)
                        {
                            Content = new StringContent(testResponse.Content)
                        };
                    }

                    // If we have a last response, repeat it (useful for retry scenarios)
                    if (_lastResponse != null)
                    {
                        return new HttpResponseMessage(_lastResponse.StatusCode)
                        {
                            Content = new StringContent(_lastResponse.Content)
                        };
                    }

                    // Default response if nothing configured
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("Default response")
                    };
                }

                protected override void Dispose(bool disposing)
                {
                    if (disposing)
                    {
                        // Clear any remaining responses to prevent memory leaks
                        _responses.Clear();
                        _exceptions.Clear();
                        _lastResponse = null;
                    }
                    base.Dispose(disposing);
                }
            }

            private class TestResponse
            {
                public HttpStatusCode StatusCode { get; set; }
                public string Content { get; set; }
            }
        }
    } 
} 
