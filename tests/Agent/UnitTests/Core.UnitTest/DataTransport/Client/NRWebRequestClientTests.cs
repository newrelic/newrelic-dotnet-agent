// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NUnit.Framework;
using Telerik.JustMock;
using Telerik.JustMock.AutoMock.Ninject.Activation;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    [TestFixture]
    public class NRWebRequestClientTests
    {
        private IWebProxy _proxy;
        private IHttpRequest _request;
        private NRWebRequestClient _client;
        private IConnectionInfo _mockConnectionInfo;
        private IConfiguration _mockConfiguration;

        [SetUp]
        public void Setup()
        {
            _mockConfiguration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _mockConfiguration.AgentLicenseKey).Returns( "12345");
            Mock.Arrange(() => _mockConfiguration.AgentRunId).Returns("123");
            Mock.Arrange(() => _mockConfiguration.CollectorMaxPayloadSizeInBytes).Returns(int.MaxValue);
            Mock.Arrange(() => _mockConfiguration.PutForDataSend).Returns(false);

            _mockConnectionInfo = Mock.Create<IConnectionInfo>();
            Mock.Arrange(() => _mockConnectionInfo.Host).Returns("testhost.com");
            Mock.Arrange(() => _mockConnectionInfo.HttpProtocol).Returns("https");
            Mock.Arrange(() => _mockConnectionInfo.Port).Returns(123);

            _proxy = Mock.Create<IWebProxy>();

            _request = CreateHttpRequest();

            _client = new NRWebRequestClient(_proxy, _mockConfiguration);
        }

        [Test]
        public async Task SendAsync_ShouldReturnValidResponse_WhenWebRequestIsSuccessful()
        {
            // Arrange
            var fakeResponse = Mock.Create<HttpWebResponse>();
            _client.SetHttpWebRequestFunc(uri =>
            {
                var mockWebRequest = Mock.Create<HttpWebRequest>();
                Mock.Arrange(() => mockWebRequest.GetRequestStream()).Returns(new MemoryStream());
                Mock.Arrange(() => mockWebRequest.GetResponseAsync()).TaskResult((WebResponse)fakeResponse);
                return mockWebRequest;
            });

            // Act
            var response = await _client.SendAsync(_request);

            // Assert
            Assert.IsNotNull(response);
        }

        [Test]
        public void SendAsync_ShouldThrow_WhenNullOutputStream()
        {
            // Arrange
            _client.SetHttpWebRequestFunc(uri =>
            {
                var mockWebRequest = Mock.Create<HttpWebRequest>();
                Mock.Arrange(() => mockWebRequest.GetRequestStream()).Returns(() => null);
                return mockWebRequest;
            });

            // Act & Assert
            Assert.ThrowsAsync<NullReferenceException>(() => _client.SendAsync(_request));
        }

        [Test]
        public void SendAsync_ThrowsWebException_WhenWebExceptionResponseIsNull()
        {
            // Arrange
            _client.SetHttpWebRequestFunc(uri =>
            {
                var mockWebRequest = Mock.Create<HttpWebRequest>();
                Mock.Arrange(() => mockWebRequest.Address).Returns(new Uri("https://sometesthost.com"));
                var webException = new WebException("testing");
                Mock.Arrange(() => mockWebRequest.GetResponseAsync()).Throws(webException);
                return mockWebRequest;
            });

            // Act & Assert
            Assert.ThrowsAsync<WebException>(() => _client.SendAsync(_request));
        }
        [Test]
        public async Task SendAsync_ReturnsResponse_WhenWebExceptionResponseIsNotNull()
        {
            // Arrange
            _client.SetHttpWebRequestFunc(uri =>
            {
                var mockWebRequest = Mock.Create<HttpWebRequest>();
                Mock.Arrange(() => mockWebRequest.Address).Returns(new Uri("https://sometesthost.com"));

                var mockHttpWebResponse = Mock.Create<HttpWebResponse>();
                Mock.Arrange(() => mockHttpWebResponse.StatusCode).Returns(HttpStatusCode.BadRequest);
                Mock.Arrange(() => mockHttpWebResponse.StatusDescription).Returns("Bad Request");
                var webException = new WebException("testing", null, WebExceptionStatus.SendFailure,mockHttpWebResponse);
                Mock.Arrange(() => mockWebRequest.GetResponseAsync()).Throws(webException);
                return mockWebRequest;
            });

            // Act
            var response = await _client.SendAsync(_request);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
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
