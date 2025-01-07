// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Telerik.JustMock;


namespace NewRelic.Agent.Core.DataTransport.Client
{
    [TestFixture]
    public class HttpClientWrapperTests
    {
        private HttpClientWrapper _httpClientWrapper;
        private HttpClient _mockHttpClient;
        private int _timeoutMilliseconds = 1000;

        [SetUp]
        public void SetUp()
        {
            _mockHttpClient = Mock.Create<HttpClient>();
            _httpClientWrapper = new HttpClientWrapper(_mockHttpClient, _timeoutMilliseconds);
        }

        [TearDown]
        public void TearDown()
        {
            _httpClientWrapper.Dispose();
            _mockHttpClient.Dispose();
        }

        [Test]
        public async Task SendAsync_ShouldReturnHttpResponseMessageWrapper_OnSuccess()
        {
            // Arrange
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, "http://example.com");
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            Mock.Arrange(() => _mockHttpClient.SendAsync(Arg.IsAny<HttpRequestMessage>(), Arg.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            // Act
            var result = await _httpClientWrapper.SendAsync(requestMessage);

            // Assert
            Assert.That(result.IsSuccessStatusCode, Is.True);
        }

        [Test]
        public void SendAsync_ShouldThrowException_OnFailure()
        {
            // Arrange
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, "http://example.com");
            Mock.Arrange(() => _mockHttpClient.SendAsync(Arg.IsAny<HttpRequestMessage>(), Arg.IsAny<CancellationToken>()))
                .Throws(new HttpRequestException("Request failed"));

            // Act & Assert
            Assert.ThrowsAsync<HttpRequestException>(async () => await _httpClientWrapper.SendAsync(requestMessage));
        }

        [Test]
        public void SendAsync_ShouldThrowException_OnTimeout()
        {
            // Arrange
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, "http://example.com");
            Mock.Arrange(() => _mockHttpClient.SendAsync(Arg.IsAny<HttpRequestMessage>(), Arg.IsAny<CancellationToken>()))
                .Returns(async (HttpRequestMessage msg, CancellationToken token) =>
                {
                    await Task.Delay(_timeoutMilliseconds + 1000, token);
                    return new HttpResponseMessage(HttpStatusCode.RequestTimeout);
                });

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () => await _httpClientWrapper.SendAsync(requestMessage));
        }
    }
}
#endif
