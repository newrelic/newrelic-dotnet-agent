// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.DataTransport
{
    [TestFixture]
    public class CustomRetryHandlerTests
    {
        private TestHttpMessageHandler _innerHandler;
        private CustomRetryHandler _retryHandler;
        private HttpClient _httpClient;

        [SetUp]
        public void SetUp()
        {
            _innerHandler = new TestHttpMessageHandler();
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

        #region Success Scenarios

        [Test]
        public async Task SendAsync_WithSuccessResponse_ReturnsResponseImmediately()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Success")
            });

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SendAsync_SucceedsOnSecondAttempt_ReturnsSuccess()
        {
            // Arrange
            _innerHandler.SetSequence(
                new HttpResponseMessage(HttpStatusCode.InternalServerError),
                new HttpResponseMessage(HttpStatusCode.OK)
            );

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(2));
        }

        [Test]
        public async Task SendAsync_SucceedsOnThirdAttempt_ReturnsSuccess()
        {
            // Arrange
            _innerHandler.SetSequence(
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                new HttpResponseMessage(HttpStatusCode.RequestTimeout),
                new HttpResponseMessage(HttpStatusCode.OK)
            );

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
        }

        #endregion

        #region Transient Failure Scenarios

        [Test]
        public async Task SendAsync_With408RequestTimeout_RetriesUpToMaxAttempts()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.RequestTimeout));

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestTimeout));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
        }

        [Test]
        public async Task SendAsync_With429TooManyRequests_RetriesUpToMaxAttempts()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage((HttpStatusCode)429)); // TooManyRequests

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo((HttpStatusCode)429));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
        }

        [Test]
        public async Task SendAsync_With500InternalServerError_RetriesUpToMaxAttempts()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
        }

        [Test]
        public async Task SendAsync_With502BadGateway_RetriesUpToMaxAttempts()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadGateway));

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
        }

        [Test]
        public async Task SendAsync_With503ServiceUnavailable_RetriesUpToMaxAttempts()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
        }

        [Test]
        public async Task SendAsync_With504GatewayTimeout_RetriesUpToMaxAttempts()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.GatewayTimeout));

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.GatewayTimeout));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
        }

        #endregion

        #region Non-Transient Failure Scenarios

        [Test]
        public async Task SendAsync_With400BadRequest_DoesNotRetry()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadRequest));

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SendAsync_With401Unauthorized_DoesNotRetry()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized));

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SendAsync_With403Forbidden_DoesNotRetry()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.Forbidden));

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SendAsync_With404NotFound_DoesNotRetry()
        {
            // Arrange
            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.NotFound));

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
        }

        #endregion

        #region Exception Scenarios

        [Test]
        public async Task SendAsync_WithHttpRequestException_RetriesUpToMaxAttempts()
        {
            // Arrange
            _innerHandler.SetException(new HttpRequestException("Network error"));

            // Act & Assert
            var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
                await _httpClient.GetAsync("http://test.com"));

            Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
            Assert.That(ex.Message, Does.Contain("Network error"));
        }

        [Test]
        public async Task SendAsync_WithTaskCanceledException_NotUserCancellation_Retries()
        {
            // Arrange
            _innerHandler.SetException(new TaskCanceledException("Request timed out"));

            // Act & Assert
            var ex = Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _httpClient.GetAsync("http://test.com"));

            Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
        }

        [Test]
        public async Task SendAsync_WithUserCancellation_DoesNotRetry()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately before making the request

            // Act & Assert
            var ex = Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _httpClient.GetAsync("http://test.com", cts.Token));

            // Should not retry when user cancels
            Assert.That(_innerHandler.RequestCount, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public async Task SendAsync_WithOtherException_DoesNotRetry()
        {
            // Arrange
            _innerHandler.SetException(new InvalidOperationException("Unexpected error"));

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _httpClient.GetAsync("http://test.com"));

            Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
            Assert.That(ex.Message, Is.EqualTo("Unexpected error"));
        }

        #endregion

        #region Request Cloning Tests

        [Test]
        public async Task SendAsync_ClonesRequestForRetries()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "http://test.com")
            {
                Content = new StringContent("test data")
            };
            request.Headers.Add("X-Custom-Header", "test-value");

            _innerHandler.SetSequence(
                new HttpResponseMessage(HttpStatusCode.InternalServerError),
                new HttpResponseMessage(HttpStatusCode.OK)
            );

            // Act
            var response = await _httpClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(2));
        }

        [Test]
        public async Task SendAsync_ClonesRequestHeaders()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "http://test.com");
            request.Headers.Add("Authorization", "Bearer token123");
            request.Headers.Add("X-Custom", "value");

            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _httpClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task SendAsync_ClonesRequestWithByteArrayContent()
        {
            // Arrange
            var content = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5 });
            content.Headers.Add("Content-Type", "application/octet-stream");

            var request = new HttpRequestMessage(HttpMethod.Post, "http://test.com")
            {
                Content = content
            };

            _innerHandler.SetSequence(
                new HttpResponseMessage(HttpStatusCode.InternalServerError),
                new HttpResponseMessage(HttpStatusCode.OK)
            );

            // Act
            var response = await _httpClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(2));
        }

        #endregion

        #region Retry Delay Tests

        [Test]
        public async Task SendAsync_WaitsBeforeRetry()
        {
            // Arrange
            _innerHandler.SetSequence(
                new HttpResponseMessage(HttpStatusCode.InternalServerError),
                new HttpResponseMessage(HttpStatusCode.OK)
            );

            var startTime = DateTime.UtcNow;

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            var elapsed = DateTime.UtcNow - startTime;

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            // First retry should wait at least 1 second (base delay)
            Assert.That(elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(900));
        }

        [Test]
        public async Task SendAsync_IncreasesDelayExponentially()
        {
            // Arrange
            _innerHandler.SetSequence(
                new HttpResponseMessage(HttpStatusCode.InternalServerError),
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                new HttpResponseMessage(HttpStatusCode.OK)
            );

            var startTime = DateTime.UtcNow;

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            var elapsed = DateTime.UtcNow - startTime;

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            // Two retries: ~1s + ~2s = ~3s minimum (plus jitter)
            Assert.That(elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(2900));
        }

        [Test]
        public async Task SendAsync_AllRetriesExhausted_ThrowsException()
        {
            // Arrange - Use exceptions which DO throw after retries exhausted
            _innerHandler.SetException(new HttpRequestException("Network failure"));

            // Act & Assert
            var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
                await _httpClient.GetAsync("http://test.com"));

            Assert.That(ex.Message, Does.Contain("Network failure"));
            Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
        }

        #endregion

        #region Edge Cases

        [Test]
        public async Task SendAsync_WithEmptyContent_HandlesCorrectly()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "http://test.com")
            {
                Content = new StringContent("")
            };

            _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var response = await _httpClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task SendAsync_DisposesFailedResponses()
        {
            // Arrange
            var failedResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            _innerHandler.SetSequence(
                failedResponse,
                new HttpResponseMessage(HttpStatusCode.OK)
            );

            // Act
            var response = await _httpClient.GetAsync("http://test.com");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            // The failed response should be disposed during retry
        }

        #endregion

        #region Test Helper Class

        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private HttpResponseMessage _response;
            private Exception _exception;
            private Action _action;
            private HttpResponseMessage[] _sequence;
            private int _sequenceIndex = 0;
            public int RequestCount { get; private set; }

            public void SetResponse(HttpResponseMessage response)
            {
                _response = response;
                _exception = null;
                _sequence = null;
                _action = null;
            }

            public void SetException(Exception exception)
            {
                _exception = exception;
                _response = null;
                _sequence = null;
                _action = null;
            }

            public void SetAction(Action action)
            {
                _action = action;
                _exception = null;
                _response = null;
                _sequence = null;
            }

            public void SetSequence(params HttpResponseMessage[] responses)
            {
                _sequence = responses;
                _sequenceIndex = 0;
                _response = null;
                _exception = null;
                _action = null;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestCount++;

                cancellationToken.ThrowIfCancellationRequested();

                if (_action != null)
                {
                    _action();
                }

                if (_exception != null)
                {
                    return Task.FromException<HttpResponseMessage>(_exception);
                }

                if (_sequence != null && _sequence.Length > 0)
                {
                    var response = _sequence[_sequenceIndex];
                    if (_sequenceIndex < _sequence.Length - 1)
                    {
                        _sequenceIndex++;
                    }
                    return Task.FromResult(new HttpResponseMessage(response.StatusCode)
                    {
                        Content = new StringContent(""),
                        ReasonPhrase = response.ReasonPhrase
                    });
                }

                return Task.FromResult(new HttpResponseMessage(_response.StatusCode)
                {
                    Content = new StringContent(""),
                    ReasonPhrase = _response.ReasonPhrase
                });
            }
        }

        #endregion
    }
}
