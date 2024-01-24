// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http;
using System.Net;
using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using System.Collections.Generic;
using NUnit.Framework;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Logging;
using Serilog;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class HttpCollectorWireTests
    {
        private IConfiguration _configuration;
        private IAgentHealthReporter _agentHealthReporter;
        private IHttpClientFactory _httpClientFactory;
        private ILogger _mockILogger;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _httpClientFactory = Mock.Create<IHttpClientFactory>();

            _mockILogger = Mock.Create<ILogger>();
            Log.Logger = _mockILogger;
        }

        private HttpCollectorWire CreateHttpCollectorWire(Dictionary<string, string> requestHeadersMap = null)
        {
            return new HttpCollectorWire(_configuration, requestHeadersMap, _agentHealthReporter, _httpClientFactory);
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void SendData_ShouldSendRequestWithValidParameters(bool usePutForSend)
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);
            Mock.Arrange(() => _configuration.PutForDataSend).Returns(usePutForSend);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";

            var expected = "{}";

            var mockHttpResponse = Mock.Create<IHttpResponse>();
            Mock.Arrange(() => mockHttpResponse.StatusCode).Returns(HttpStatusCode.OK);
            Mock.Arrange(() => mockHttpResponse.IsSuccessStatusCode).Returns(true);
            Mock.Arrange(() => mockHttpResponse.GetContentAsync()).ReturnsAsync(expected);

            var mockHttpClient = Mock.Create<IHttpClient>();
            Mock.Arrange(() => mockHttpClient.SendAsync(Arg.IsAny<IHttpRequest>())).ReturnsAsync(mockHttpResponse);

            Mock.Arrange(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>())).Returns(mockHttpClient);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var response = collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid());

            // Assert
            Assert.That(response, Is.EqualTo(expected));
            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>()), Occurs.Once());
        }

        [Test]
        public void SendData_ShouldThrowHttpRequestException_WhenRequestThrows()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";

            var expected = "{}";
            var mockHttpResponse = Mock.Create<IHttpResponse>();
            Mock.Arrange(() => mockHttpResponse.StatusCode).Returns(HttpStatusCode.OK);
            Mock.Arrange(() => mockHttpResponse.IsSuccessStatusCode).Returns(true);
            Mock.Arrange(() => mockHttpResponse.GetContentAsync()).ReturnsAsync(expected);

            var mockHttpClient = Mock.Create<IHttpClient>();
            Mock.Arrange(() => mockHttpClient.SendAsync(Arg.IsAny<IHttpRequest>())).Throws(new HttpRequestException());

            Mock.Arrange(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>())).Returns(mockHttpClient);

            var collectorWire = CreateHttpCollectorWire();

            // Act and Assert
            Assert.Throws<HttpRequestException>(() => collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid()));

            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>()), Occurs.Once());
        }

        [Test]
        public void SendData_ShouldThrowHttpException_WhenResponse_IsNotSuccessful()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "invalidjson";

            var expected = "{}";
            var mockHttpResponse = Mock.Create<IHttpResponse>();
            Mock.Arrange(() => mockHttpResponse.StatusCode).Returns(HttpStatusCode.InternalServerError);
            Mock.Arrange(() => mockHttpResponse.IsSuccessStatusCode).Returns(false);
            Mock.Arrange(() => mockHttpResponse.GetContentAsync()).ReturnsAsync(expected);

            var mockHttpClient = Mock.Create<IHttpClient>();
            Mock.Arrange(() => mockHttpClient.SendAsync(Arg.IsAny<IHttpRequest>())).ReturnsAsync(mockHttpResponse);

            Mock.Arrange(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>())).Returns(mockHttpClient);

            var collectorWire = CreateHttpCollectorWire();

            // Act and Assert
            Assert.Throws<HttpException>(() => collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid()));

            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>()), Occurs.Once());
        }

        [Test]
        public void SendData_ShouldDropPayload_WhenPayloadSizeExceedsMaxSize()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024);
            Mock.Arrange(() => _configuration.CompressedContentEncoding).Returns("gzip");

            var connectionInfo = new ConnectionInfo(_configuration);
            var largeSerializedData = new string('x', 1024 * 1024 + 1); // Create a string larger than the maximum allowed payload size


            var expected = "{}";
            var mockHttpResponse = Mock.Create<IHttpResponse>();
            Mock.Arrange(() => mockHttpResponse.StatusCode).Returns(HttpStatusCode.OK);
            Mock.Arrange(() => mockHttpResponse.IsSuccessStatusCode).Returns(true);
            Mock.Arrange(() => mockHttpResponse.GetContentAsync()).ReturnsAsync(expected);

            var mockHttpClient = Mock.Create<IHttpClient>();
            Mock.Arrange(() => mockHttpClient.SendAsync(Arg.IsAny<IHttpRequest>())).ReturnsAsync(mockHttpResponse);

            Mock.Arrange(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>())).Returns(mockHttpClient);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var result = collectorWire.SendData("test_method", connectionInfo, largeSerializedData, Guid.NewGuid());

            // Assert
            Assert.That(result, Is.EqualTo("{}"));
            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityPayloadsDroppeDueToMaxPayloadSizeLimit("test_method"), Occurs.Once());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SendData_ShouldNotCallAuditLog_UnlessAuditLogIsEnabled(bool isEnabled)
        {
            // Arrange
            AuditLog.ResetLazyLogger();
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";

            var expected = "{}";

            var mockHttpResponse = Mock.Create<IHttpResponse>();
            Mock.Arrange(() => mockHttpResponse.StatusCode).Returns(HttpStatusCode.OK);
            Mock.Arrange(() => mockHttpResponse.IsSuccessStatusCode).Returns(true);
            Mock.Arrange(() => mockHttpResponse.GetContentAsync()).ReturnsAsync(expected);

            var mockHttpClient = Mock.Create<IHttpClient>();
            Mock.Arrange(() => mockHttpClient.SendAsync(Arg.IsAny<IHttpRequest>())).ReturnsAsync(mockHttpResponse);

            Mock.Arrange(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>(), Arg.IsAny<IConfiguration>())).Returns(mockHttpClient);

            var collectorWire = CreateHttpCollectorWire();

            var mockForContextLogger = Mock.Create<ILogger>();
            Mock.Arrange(() => _mockILogger.ForContext(Arg.AnyString, Arg.AnyObject, false))
                .Returns(() => mockForContextLogger);

            AuditLog.IsAuditLogEnabled = isEnabled;

            // Act
            _ = collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid());

            // Assert
            Mock.Assert(() => mockForContextLogger.Fatal(Arg.AnyString), isEnabled ? Occurs.Exactly(3) : Occurs.Never());
        }
    }
}
