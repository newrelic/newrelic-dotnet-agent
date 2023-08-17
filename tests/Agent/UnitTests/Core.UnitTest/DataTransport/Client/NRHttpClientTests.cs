// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
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
            Mock.Arrange(() => _mockConfiguration.AgentLicenseKey).Returns( "12345");
            Mock.Arrange(() => _mockConfiguration.AgentRunId).Returns("123");
            Mock.Arrange(() => _mockConfiguration.CollectorMaxPayloadSizeInBytes).Returns(int.MaxValue);

            _mockConnectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => _mockConnectionInfo.Host).Returns("testhost.com");
            Mock.Arrange(() => _mockConnectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => _mockConnectionInfo.Port).Returns(123);


            _mockProxy = Mock.Create<IWebProxy>();
            _mockHttpClientWrapper = Mock.Create<IHttpClientWrapper>();

            _client = new NRHttpClient(_mockProxy, _mockConfiguration);
            _client.SetHttpClientWrapper(_mockHttpClientWrapper); // Inject the mock HttpClient wrapper
        }

        [Test]
        public async Task SendAsync_ReturnsResponse_WhenSendAsyncSucceeds()
        {
            // Arrange
            var request = CreateHttpRequest();

            var mockHttpResponseMessage = Mock.Create<IHttpResponseMessageWrapper>();
            Mock.Arrange(() => mockHttpResponseMessage.StatusCode).Returns(HttpStatusCode.OK);
            Mock.Arrange(() => mockHttpResponseMessage.IsSuccessStatusCode).Returns(true);

            Mock.Arrange(() => _mockHttpClientWrapper.SendAsync(Arg.IsAny<HttpRequestMessage>()))
                .TaskResult(mockHttpResponseMessage);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            Assert.IsNotNull(response);
        }

        [Test]
        public void SendAsync_ThrowsHttpRequestException_WhenSendAsyncThrows()
        {
            // Arrange
            var request = CreateHttpRequest();

            Mock.Arrange(() => _mockHttpClientWrapper.SendAsync(Arg.IsAny<HttpRequestMessage>()))
                .Throws<HttpRequestException>();

            // Act & Assert
            Assert.ThrowsAsync<HttpRequestException>(() => _client.SendAsync(request));
        }

        private IHttpRequest CreateHttpRequest()
        {
            var request = new HttpRequest(_mockConfiguration)
            {
                Endpoint = "Test",
                ConnectionInfo = _mockConnectionInfo,
                RequestGuid = Guid.NewGuid(),
                Content = { SerializedData = "{\"Test\"}", ContentType = "application/json"}
            };

            return request;
        }
    }
}
#endif
