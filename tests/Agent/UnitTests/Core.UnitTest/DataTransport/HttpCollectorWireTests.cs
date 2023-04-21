// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http;
using System.Net;
using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using System.Collections.Generic;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Threading;
using NewRelic.Agent.Core.Exceptions;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class HttpCollectorWireTests
    {
        private IConfiguration _configuration;
        private IAgentHealthReporter _agentHealthReporter;
        private IHttpClientFactory _httpClientFactory;
        private MockHttpMessageHandler _mockHttpMessageHandler;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _httpClientFactory = Mock.Create<IHttpClientFactory>();
        }

        private HttpCollectorWire CreateHttpCollectorWire(Dictionary<string, string> requestHeadersMap = null)
        {
            return new HttpCollectorWire(_configuration, requestHeadersMap, _agentHealthReporter, _httpClientFactory);
        }

        private void CreateMockHttpClient(HttpResponseMessage response, bool throwHttpRequestException)
        {
            _mockHttpMessageHandler = new MockHttpMessageHandler(response, throwHttpRequestException);
            var httpClient = new HttpClient(_mockHttpMessageHandler);

            Mock.Arrange(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>())).Returns(httpClient);
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
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };

            CreateMockHttpClient(httpResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var response = collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid());

            // Assert
            Assert.AreEqual("{}", response);
            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>()), Occurs.Once());
            Assert.AreEqual(true, _mockHttpMessageHandler.SendAsyncInvoked);
        }

        [Test]
        public void SendData_ShouldThrowHttpRequestException_WhenRequestFails()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{}")
            };

            CreateMockHttpClient(httpResponse, true);

            var collectorWire = CreateHttpCollectorWire();

            // Act and Assert
            Assert.Throws<HttpRequestException>(() => collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid()));

            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>()), Occurs.Once());
            Assert.AreEqual(true, _mockHttpMessageHandler.SendAsyncInvoked);
        }

        [Test]
        public void SendData_ShouldThrowHttpException_WhenResponse_IsNotSuccessful()
        {
            // Arrange
            Mock.Arrange(() => _configuration.AgentLicenseKey).Returns("license_key");
            Mock.Arrange(() => _configuration.CollectorTimeout).Returns(5000);
            Mock.Arrange(() => _configuration.CollectorMaxPayloadSizeInBytes).Returns(1024 * 1024);

            var connectionInfo = new ConnectionInfo(_configuration);
            var serializedData = "{ \"key\": \"value\" }";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{}")
            };

            CreateMockHttpClient(httpResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            // Act and Assert
            Assert.Throws<HttpException>(() => collectorWire.SendData("test_method", connectionInfo, serializedData, Guid.NewGuid()));

            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>()), Occurs.Once());
            Assert.AreEqual(true, _mockHttpMessageHandler.SendAsyncInvoked);
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
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };

            CreateMockHttpClient(httpResponse, false);

            var collectorWire = CreateHttpCollectorWire();

            // Act
            var result = collectorWire.SendData("test_method", connectionInfo, largeSerializedData, Guid.NewGuid());

            // Assert
            Assert.AreEqual("{}", result);
            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityPayloadsDroppeDueToMaxPayloadSizeLimit("test_method"), Occurs.Once());
            Mock.Assert(() => _httpClientFactory.CreateClient(Arg.IsAny<IWebProxy>()), Occurs.Never());
            Assert.AreEqual(false, _mockHttpMessageHandler.SendAsyncInvoked);
        }
    }

    public class MockHttpMessageHandler: HttpMessageHandler
    {
        private readonly HttpResponseMessage _mockResponse;
        private readonly bool _throwHttpRequestException;

        public MockHttpMessageHandler(HttpResponseMessage mockResponse, bool throwHttpRequestException)
        {
            _mockResponse = mockResponse;
            _throwHttpRequestException = throwHttpRequestException;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendAsyncInvoked = true;

            if (_throwHttpRequestException)
                throw new HttpRequestException();
            
            return Task.FromResult(_mockResponse);
        }

        /// <summary>
        /// Indicates if SendAsync was called. Required because we can't Assert on protected methods
        /// </summary>
        public bool SendAsyncInvoked { get; private set; }
    }
}
