// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http;
using System.Net;
using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using Moq;
using System.Collections.Generic;
using NUnit.Framework;
using Moq.Protected;
using System.Threading.Tasks;
using System.Threading;
using NewRelic.Agent.Core.Attributes.Tests.Models;
using NewRelic.Agent.Core.Exceptions;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class HttpCollectorWireTests
    {
        private Mock<IConfiguration> _configurationMock;
        private Mock<IAgentHealthReporter> _agentHealthReporterMock;
        private Mock<IHttpClientFactory> _httpClientFactoryMock;
        private Mock<HttpMessageHandler> _httpMessageHandlerMock;

        [SetUp]
        public void SetUp()
        {
            _configurationMock = new Mock<IConfiguration>();
            _agentHealthReporterMock = new Mock<IAgentHealthReporter>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();

            // Mocking the HttpMessageHandler to be able to mock the HttpClient's SendAsync method
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        }

        private HttpCollectorWire CreateHttpCollectorWire(Dictionary<string, string> requestHeadersMap = null)
        {
            return new HttpCollectorWire(_configurationMock.Object, requestHeadersMap, _agentHealthReporterMock.Object, _httpClientFactoryMock.Object);
        }

        private void CreateMockHttpClient(HttpResponseMessage response)
        {
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response);

            var httpClient = new HttpClient(_httpMessageHandlerMock.Object);

            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<IWebProxy>())).Returns(httpClient);
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void SendData_ShouldSendRequestWithValidParameters(bool usePutForSend)
        {
            // Arrange
            _configurationMock.Setup(x => x.AgentLicenseKey).Returns("license_key");
            _configurationMock.Setup(x => x.CollectorTimeout).Returns(5000);
            _configurationMock.Setup(x => x.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);
            _configurationMock.Setup(x => x.PutForDataSend).Returns(usePutForSend);

            var connectionInfo = new ConnectionInfo(_configurationMock.Object);
            var serializedData = "{ \"key\": \"value\" }";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };

            CreateMockHttpClient(httpResponse);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var response = collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid());

            // Assert
            Assert.AreEqual("{}", response);
            _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<IWebProxy>()), Times.Once);
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void SendData_ShouldThrowHttpRequestException_WhenRequestFails()
        {
            // Arrange
            _configurationMock.Setup(x => x.AgentLicenseKey).Returns("license_key");
            _configurationMock.Setup(x => x.CollectorTimeout).Returns(5000);
            _configurationMock.Setup(x => x.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configurationMock.Object);
            var serializedData = "{ \"key\": \"value\" }";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{}")
            };

            CreateMockHttpClient(httpResponse);

            var collectorWire = CreateHttpCollectorWire();

            // Act and Assert
            Assert.Throws<HttpException>(() => collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid()));

            _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<IWebProxy>()), Times.Once);
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void SendData_ShouldDropPayload_WhenPayloadSizeExceedsMaxSize()
        {
            // Arrange
            _configurationMock.Setup(x => x.AgentLicenseKey).Returns("license_key");
            _configurationMock.Setup(x => x.CollectorTimeout).Returns(5000);
            _configurationMock.Setup(x => x.CollectorMaxPayloadSizeInBytes).Returns(1024);
            _configurationMock.Setup(x => x.CompressedContentEncoding).Returns("gzip");

            var connectionInfo = new ConnectionInfo(_configurationMock.Object);
            var largeSerializedData = new string('x', 1024 * 1024 + 1); // Create a string larger than the maximum allowed payload size
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };

            CreateMockHttpClient(httpResponse);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var result = collectorWire.SendData("test_method", connectionInfo, largeSerializedData, Guid.NewGuid());

            // Assert
            Assert.AreEqual("{}", result);
            _agentHealthReporterMock.Verify(x => x.ReportSupportabilityPayloadsDroppeDueToMaxPayloadSizeLimit("test_method"), Times.Once);
            _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<IWebProxy>()), Times.Never);
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }
    }
}
