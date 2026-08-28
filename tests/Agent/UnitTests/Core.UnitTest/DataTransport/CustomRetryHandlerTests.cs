// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.SharedInterfaces;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.DataTransport;

[TestFixture]
public class CustomRetryHandlerTests
{
    private const double DefaultCeilingSeconds = 5;

    private TestHttpMessageHandler _innerHandler;
    private CustomRetryHandler _retryHandler;
    private HttpClient _httpClient;
    private List<TimeSpan> _requestedDelays;

    [SetUp]
    public void SetUp()
    {
        _innerHandler = new TestHttpMessageHandler();
        _requestedDelays = new List<TimeSpan>();
        _retryHandler = new CustomRetryHandler(delayFunc: RecordDelay)
        {
            InnerHandler = _innerHandler
        };
        _httpClient = new HttpClient(_retryHandler);
    }

    private Task RecordDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        _requestedDelays.Add(delay);
        return Task.CompletedTask;
    }

    private CustomRetryHandler CreateHandler(
        IOtelBridgeSupportabilityMetricCounters counters = null,
        double ceilingSeconds = DefaultCeilingSeconds,
        Func<TimeSpan, CancellationToken, Task> delayFunc = null)
    {
        return new CustomRetryHandler(counters, TimeSpan.FromSeconds(ceilingSeconds), delayFunc ?? RecordDelay)
        {
            InnerHandler = _innerHandler
        };
    }

    private static HttpResponseMessage ResponseWithRetryAfterSeconds(HttpStatusCode statusCode, int seconds)
    {
        var response = new HttpResponseMessage(statusCode);
        response.Headers.TryAddWithoutValidation("Retry-After", seconds.ToString());
        return response;
    }

    private static HttpResponseMessage ResponseWithRetryAfterDate(HttpStatusCode statusCode, DateTimeOffset serverNow, int offsetSeconds)
    {
        var response = new HttpResponseMessage(statusCode);
        response.Headers.TryAddWithoutValidation("Date", serverNow.ToString("r"));
        response.Headers.TryAddWithoutValidation("Retry-After", serverNow.AddSeconds(offsetSeconds).ToString("r"));
        return response;
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

        // Act
        var response = await _httpClient.GetAsync("http://test.com");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        // First retry waits the 1s base delay plus up to 500ms of jitter -- upper bound included so a
        // regression that inflates the requested delay fails instead of silently slowing exports.
        Assert.That(_requestedDelays, Has.Count.EqualTo(1));
        Assert.That(_requestedDelays[0].TotalMilliseconds, Is.InRange(1000, 1500));
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

        // Act
        var response = await _httpClient.GetAsync("http://test.com");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_requestedDelays, Has.Count.EqualTo(2));
        Assert.That(_requestedDelays[0].TotalMilliseconds, Is.InRange(1000, 1500));
        Assert.That(_requestedDelays[1].TotalMilliseconds, Is.InRange(2000, 2500));
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

    #region Retry-After Honoring Tests

    [Test]
    public async Task TestHttpMessageHandler_PropagatesResponseHeaders()
    {
        // Guards the fixture itself: if the stub stops copying headers onto the response it builds,
        // every Retry-After test below silently exercises the no-header path instead of failing.
        var serverNow = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        _innerHandler.SetResponse(ResponseWithRetryAfterDate(HttpStatusCode.NotFound, serverNow, 7));

        var response = await _httpClient.GetAsync("http://test.com");

        Assert.That(response.Headers.RetryAfter, Is.Not.Null);
        Assert.That(response.Headers.RetryAfter.Date, Is.EqualTo(serverNow.AddSeconds(7)));
        Assert.That(response.Headers.Date, Is.EqualTo(serverNow));
    }

    [Test]
    public async Task SendAsync_TransientResponseWithRetryAfterDeltaUnderCeiling_HonorsServerDelay()
    {
        // Arrange
        _innerHandler.SetSequence(
            ResponseWithRetryAfterSeconds((HttpStatusCode)429, 3),
            new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var response = await _httpClient.GetAsync("http://test.com");

        // Assert -- the server's 3s wins over the ~1s exponential value for this attempt
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_innerHandler.RequestCount, Is.EqualTo(2));
        Assert.That(_requestedDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(3) }));
    }

    [Test]
    public async Task SendAsync_TransientResponseWithRetryAfterHttpDateUnderCeiling_HonorsServerDelay()
    {
        // Arrange -- a server "now" far from the local clock, so a UtcNow-based computation would be wildly wrong
        var serverNow = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _innerHandler.SetSequence(
            ResponseWithRetryAfterDate(HttpStatusCode.ServiceUnavailable, serverNow, 4),
            new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var response = await _httpClient.GetAsync("http://test.com");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_requestedDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(4) }));
    }

    [Test]
    public async Task SendAsync_RetryAfterOnServiceUnavailable_HonoredSameAs429()
    {
        // Arrange -- honoring is gated on header presence, not on status code
        _innerHandler.SetSequence(
            ResponseWithRetryAfterSeconds(HttpStatusCode.ServiceUnavailable, 2),
            new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var response = await _httpClient.GetAsync("http://test.com");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_requestedDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
    }

    [Test]
    public async Task SendAsync_RetryAfterExceedsCeiling_BailsWithoutSleeping()
    {
        // Arrange
        var counters = new FakeMetricCounters();
        using var retryHandler = CreateHandler(counters);
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetResponse(ResponseWithRetryAfterSeconds((HttpStatusCode)429, 30));

        // Act
        var response = await client.GetAsync("http://test.com");

        // Assert -- no sleep, no second attempt; the caller's next periodic cycle retries instead
        Assert.That(response.StatusCode, Is.EqualTo((HttpStatusCode)429));
        Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
        Assert.That(_requestedDelays, Is.Empty);
        Assert.That(counters.Recorded, Does.Contain(OtelBridgeSupportabilityMetric.ExportFailure));
        Assert.That(counters.Recorded, Does.Not.Contain(OtelBridgeSupportabilityMetric.ExportRetry));
    }

    [Test]
    public async Task SendAsync_RetryAfterEqualsCeiling_Bails()
    {
        // Arrange -- the ceiling is exclusive: a delay exactly at the ceiling bails
        using var retryHandler = CreateHandler(ceilingSeconds: 3);
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetResponse(ResponseWithRetryAfterSeconds(HttpStatusCode.ServiceUnavailable, 3));

        // Act
        var response = await client.GetAsync("http://test.com");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
        Assert.That(_requestedDelays, Is.Empty);
    }

    [Test]
    public async Task SendAsync_RetryAfterBail_ReturnsUsableResponse()
    {
        // Arrange -- the bailed response is handed back to the caller, so it must not be disposed
        _innerHandler.SetResponse(ResponseWithRetryAfterSeconds((HttpStatusCode)429, 30));

        // Act
        var response = await _httpClient.GetAsync("http://test.com");

        // Assert
        Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty);
        Assert.That(response.Headers.RetryAfter.Delta, Is.EqualTo(TimeSpan.FromSeconds(30)));
    }

    [Test]
    public async Task SendAsync_NoRetryAfterHeader_FallsBackToExponentialBackoff()
    {
        // Arrange
        _innerHandler.SetSequence(
            new HttpResponseMessage((HttpStatusCode)429),
            new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var response = await _httpClient.GetAsync("http://test.com");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_requestedDelays, Has.Count.EqualTo(1));
        Assert.That(_requestedDelays[0].TotalMilliseconds, Is.InRange(1000, 1500));
    }

    [Test]
    public async Task SendAsync_RetryAfterDateInPast_ClockSkewFloorsAtMinimum()
    {
        // Arrange -- a Retry-After date behind the server's own Date header yields a negative interval
        var serverNow = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        _innerHandler.SetSequence(
            ResponseWithRetryAfterDate(HttpStatusCode.ServiceUnavailable, serverNow, -60),
            new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var response = await _httpClient.GetAsync("http://test.com");

        // Assert -- floored at the 100ms minimum rather than spinning the retry loop hot
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_requestedDelays, Is.EqualTo(new[] { TimeSpan.FromMilliseconds(100) }));
    }

    [Test]
    public async Task SendAsync_RetryAfterHttpDateWithoutDateHeader_ComputedAgainstLocalClock()
    {
        // Arrange -- no Date header, so the only reference point available is the local clock
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        response.Headers.TryAddWithoutValidation("Retry-After", DateTimeOffset.UtcNow.AddSeconds(2).ToString("r"));
        _innerHandler.SetSequence(response, new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var result = await _httpClient.GetAsync("http://test.com");

        // Assert -- HTTP-date has one-second resolution, so allow the truncated second
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_requestedDelays, Has.Count.EqualTo(1));
        Assert.That(_requestedDelays[0].TotalMilliseconds, Is.InRange(1000, 2000));
    }

    [Test]
    public async Task SendAsync_WithDefaultDelayFunc_ActuallyWaitsAndRetries()
    {
        // Arrange -- no injected delay seam, so the real Task.Delay runs. A past Retry-After date floors
        // the wait at 100ms, which keeps this the one test that touches the clock cheap.
        var serverNow = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        using var retryHandler = new CustomRetryHandler { InnerHandler = _innerHandler };
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetSequence(
            ResponseWithRetryAfterDate(HttpStatusCode.ServiceUnavailable, serverNow, -60),
            new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var startTime = DateTime.UtcNow;
        var response = await client.GetAsync("http://test.com");
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_innerHandler.RequestCount, Is.EqualTo(2));
        Assert.That(elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(90));
    }

    [Test]
    public async Task SendAsync_RetryAfterOnFinalAttempt_DoesNotBail()
    {
        // Arrange -- the header arrives when retries are already exhausted, so the normal
        // exhaustion path (not the bail path) reports the failure
        var counters = new FakeMetricCounters();
        using var retryHandler = CreateHandler(counters);
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetSequence(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            ResponseWithRetryAfterSeconds(HttpStatusCode.ServiceUnavailable, 30));

        // Act
        var response = await client.GetAsync("http://test.com");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(_innerHandler.RequestCount, Is.EqualTo(3));
        Assert.That(counters.Recorded, Does.Contain(OtelBridgeSupportabilityMetric.ExportFailure));
    }

    [Test]
    public void SendAsync_TimeoutDuringRetryDelay_RecordsExportFailure()
    {
        // Arrange -- HttpClient.Timeout firing while the backoff sleep is in flight
        var counters = new FakeMetricCounters();
        using var retryHandler = CreateHandler(counters, delayFunc: (delay, token) => throw new TaskCanceledException("timed out during delay"));
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        // Act & Assert -- the failure is counted and still surfaces to the caller
        Assert.ThrowsAsync<TaskCanceledException>(async () => await client.GetAsync("http://test.com"));
        Assert.That(counters.Recorded, Does.Contain(OtelBridgeSupportabilityMetric.ExportFailure));
        Assert.That(_innerHandler.RequestCount, Is.EqualTo(1));
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

    #region Supportability Metric Tests

    [Test]
    public async Task SendAsync_OnSuccess_RecordsExportSuccess()
    {
        // Arrange
        var counters = new FakeMetricCounters();
        using var retryHandler = CreateHandler(counters);
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await client.GetAsync("http://test.com");

        // Assert
        Assert.That(counters.Recorded, Does.Contain(OtelBridgeSupportabilityMetric.ExportSuccess));
        Assert.That(counters.Recorded, Does.Not.Contain(OtelBridgeSupportabilityMetric.ExportFailure));
        Assert.That(counters.Recorded, Does.Not.Contain(OtelBridgeSupportabilityMetric.ExportRetry));
    }

    [Test]
    public async Task SendAsync_OnRetry_RecordsExportRetryAndSuccess()
    {
        // Arrange
        var counters = new FakeMetricCounters();
        using var retryHandler = CreateHandler(counters);
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetSequence(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await client.GetAsync("http://test.com");

        // Assert
        Assert.That(counters.Recorded, Does.Contain(OtelBridgeSupportabilityMetric.ExportRetry));
        Assert.That(counters.Recorded, Does.Contain(OtelBridgeSupportabilityMetric.ExportSuccess));
        Assert.That(counters.Recorded, Does.Not.Contain(OtelBridgeSupportabilityMetric.ExportFailure));
    }

    [Test]
    public async Task SendAsync_OnTransientFailureExhaustion_RecordsExportFailure()
    {
        // Arrange
        var counters = new FakeMetricCounters();
        using var retryHandler = CreateHandler(counters);
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        // Act
        await client.GetAsync("http://test.com");

        // Assert
        Assert.That(counters.Recorded, Does.Contain(OtelBridgeSupportabilityMetric.ExportFailure));
        Assert.That(counters.Recorded, Does.Not.Contain(OtelBridgeSupportabilityMetric.ExportSuccess));
    }

    [Test]
    public async Task SendAsync_OnExceptionExhaustion_RecordsExportFailure()
    {
        // Arrange
        var counters = new FakeMetricCounters();
        using var retryHandler = CreateHandler(counters);
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetException(new HttpRequestException("Network error"));

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(async () => await client.GetAsync("http://test.com"));
        Assert.That(counters.Recorded, Does.Contain(OtelBridgeSupportabilityMetric.ExportFailure));
        Assert.That(counters.Recorded, Does.Not.Contain(OtelBridgeSupportabilityMetric.ExportSuccess));
    }

    [Test]
    public async Task SendAsync_OnNonTransientFailure_RecordsNoExportMetrics()
    {
        // Arrange
        var counters = new FakeMetricCounters();
        using var retryHandler = CreateHandler(counters);
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadRequest));

        // Act
        await client.GetAsync("http://test.com");

        // Assert
        Assert.That(counters.Recorded, Does.Not.Contain(OtelBridgeSupportabilityMetric.ExportFailure));
        Assert.That(counters.Recorded, Does.Not.Contain(OtelBridgeSupportabilityMetric.ExportSuccess));
        Assert.That(counters.Recorded, Does.Not.Contain(OtelBridgeSupportabilityMetric.ExportRetry));
    }

    [Test]
    public async Task SendAsync_TwoRetries_RecordsTwoExportRetries()
    {
        // Arrange
        var counters = new FakeMetricCounters();
        using var retryHandler = CreateHandler(counters);
        using var client = new HttpClient(retryHandler);
        _innerHandler.SetSequence(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await client.GetAsync("http://test.com");

        // Assert — two retries before final success
        Assert.That(counters.Recorded.FindAll(m => m == OtelBridgeSupportabilityMetric.ExportRetry), Has.Count.EqualTo(2));
        Assert.That(counters.Recorded, Does.Contain(OtelBridgeSupportabilityMetric.ExportSuccess));
    }

    private class FakeMetricCounters : IOtelBridgeSupportabilityMetricCounters
    {
        public List<OtelBridgeSupportabilityMetric> Recorded { get; } = new();

        public void Record(OtelBridgeSupportabilityMetric metric) => Recorded.Add(metric);
        public void CollectMetrics() { }
        public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate) { }
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
                var sequenced = _sequence[_sequenceIndex];
                if (_sequenceIndex < _sequence.Length - 1)
                {
                    _sequenceIndex++;
                }
                return Task.FromResult(Rebuild(sequenced));
            }

            return Task.FromResult(Rebuild(_response));
        }

        // The handler under test must see the configured response headers (Retry-After, Date), so they
        // are copied onto the fresh instance the same way CustomRetryHandler clones request headers.
        private static HttpResponseMessage Rebuild(HttpResponseMessage configured)
        {
            var rebuilt = new HttpResponseMessage(configured.StatusCode)
            {
                Content = new StringContent(""),
                ReasonPhrase = configured.ReasonPhrase
            };

            foreach (var header in configured.Headers)
            {
                rebuilt.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return rebuilt;
        }
    }

    #endregion
}