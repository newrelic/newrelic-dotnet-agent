// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NUnit.Framework;
using Telerik.JustMock;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    [TestFixture]
    public class NRHttpClientTests
    {
        private IConfiguration _mockConfiguration;
        private IWebProxy _mockProxy;
        private NRHttpClient _client;
        private IHttpClientWrapper _mockHttpClientWrapper;
        private IConnectionInfo _mockConnectionInfo;

        [SetUp]
        public void SetUp()
        {
            _mockConfiguration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _mockConfiguration.AgentLicenseKey).Returns("12345");
            Mock.Arrange(() => _mockConfiguration.AgentRunId).Returns("123");
            Mock.Arrange(() => _mockConfiguration.CollectorMaxPayloadSizeInBytes).Returns(int.MaxValue);
            Mock.Arrange(() => _mockConfiguration.CollectorTimeout).Returns(60000); // 60 seconds
            Mock.Arrange(() => _mockConfiguration.PutForDataSend).Returns(false);

            _mockConnectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => _mockConnectionInfo.Host).Returns("testhost.com");
            Mock.Arrange(() => _mockConnectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => _mockConnectionInfo.Port).Returns(123);

            _mockProxy = Mock.Create<IWebProxy>();
            _mockHttpClientWrapper = Mock.Create<IHttpClientWrapper>();

            _client = new NRHttpClient(_mockProxy, _mockConfiguration);
            _client.SetHttpClientWrapper(_mockHttpClientWrapper); // Inject the mock HttpClient wrapper
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _mockHttpClientWrapper.Dispose();
        }

        [Test]
        public void Send_ReturnsResponse_WhenSendAsyncSucceeds()
        {
            // Arrange
            var request = CreateHttpRequest();

            var mockHttpResponseMessage = Mock.Create<IHttpResponseMessageWrapper>();
            Mock.Arrange(() => mockHttpResponseMessage.StatusCode).Returns(HttpStatusCode.OK);
            Mock.Arrange(() => mockHttpResponseMessage.IsSuccessStatusCode).Returns(true);

            Mock.Arrange(() => _mockHttpClientWrapper.SendAsync(Arg.IsAny<HttpRequestMessage>()))
                .ReturnsAsync(mockHttpResponseMessage);

            // Act
            var response = _client.Send(request);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public void Send_ThrowsHttpRequestException_WhenSendAsyncThrows()
        {
            // Arrange
            var request = CreateHttpRequest();

            Mock.Arrange(() => _mockHttpClientWrapper.SendAsync(Arg.IsAny<HttpRequestMessage>()))
                .Throws<HttpRequestException>();

            // Act & Assert
            Assert.Throws<HttpRequestException>(() => _client.Send(request));
        }

        [Test]
        public void Send_AddsCustomHeaders()
        {
            // Arrange
            var request = CreateHttpRequest();
            request.Headers.Add("Custom-Header", "HeaderValue");

            var mockHttpResponseMessage = Mock.Create<IHttpResponseMessageWrapper>();
            Mock.Arrange(() => mockHttpResponseMessage.StatusCode).Returns(HttpStatusCode.OK);
            Mock.Arrange(() => mockHttpResponseMessage.IsSuccessStatusCode).Returns(true);

            Mock.Arrange(() => _mockHttpClientWrapper.SendAsync(Arg.IsAny<HttpRequestMessage>()))
                .ReturnsAsync(mockHttpResponseMessage);

            // Act
            var response = _client.Send(request);

            // Assert
            Mock.Assert(() => _mockHttpClientWrapper.SendAsync(Arg.Matches<HttpRequestMessage>(req =>
                req.Headers.Contains("Custom-Header") && req.Headers.GetValues("Custom-Header").First() == "HeaderValue")), Occurs.Once());
        }

        [Test]
        public void Send_SetsContentHeaders()
        {
            // Arrange
            var request = CreateHttpRequest();

            var mockHttpResponseMessage = Mock.Create<IHttpResponseMessageWrapper>();
            Mock.Arrange(() => mockHttpResponseMessage.StatusCode).Returns(HttpStatusCode.OK);
            Mock.Arrange(() => mockHttpResponseMessage.IsSuccessStatusCode).Returns(true);

            Mock.Arrange(() => _mockHttpClientWrapper.SendAsync(Arg.IsAny<HttpRequestMessage>()))
                .ReturnsAsync(mockHttpResponseMessage);

            // Act
            var response = _client.Send(request);

            // Assert
            Mock.Assert(() => _mockHttpClientWrapper.SendAsync(Arg.Matches<HttpRequestMessage>(req =>
                req.Content.Headers.ContentType.MediaType == "application/json" &&
                req.Content.Headers.ContentLength == request.Content.PayloadBytes.Length)), Occurs.Once());
        }

        [Test]
        public void Dispose_DisposesHttpClientWrapper()
        {
            // Act
            _client.Dispose();

            // Assert
            Mock.Assert(() => _mockHttpClientWrapper.Dispose(), Occurs.Once());
        }

        private IHttpRequest CreateHttpRequest()
        {
            var request = new HttpRequest(_mockConfiguration)
            {
                Endpoint = "Test",
                ConnectionInfo = _mockConnectionInfo,
                RequestGuid = Guid.NewGuid(),
                Content = { SerializedData = "{\"Test\"}", ContentType = "application/json" }
            };

            return request;
        }
    }
}
#endif
