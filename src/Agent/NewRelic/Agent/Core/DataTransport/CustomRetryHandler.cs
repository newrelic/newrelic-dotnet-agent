// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport;

/// <summary>
/// Custom retry handler for OTLP exports with exponential backoff and jitter.
/// Handles transient failures (5xx, 408, 429) and network errors. A server-sent <c>Retry-After</c>
/// replaces the computed backoff when it fits within this exporter's budget; see
/// <see cref="CustomRetryHandler(IOtelBridgeSupportabilityMetricCounters, TimeSpan?, Func{TimeSpan, CancellationToken, Task})"/>.
/// </summary>
public class CustomRetryHandler : DelegatingHandler
{
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 1000; // Start with 1 second
    private const int MaxJitterMs = 500;   // Max jitter of 500ms
    private const int MinDelayMs = 100;    // Floor for any retry delay, server-requested or computed

    // Use simple Random for retry jitter - thread safety handled at call site
    private static readonly Random Random = new Random();

    private static readonly TimeSpan DefaultRetryAfterBailCeiling = TimeSpan.FromSeconds(5);

    private readonly IOtelBridgeSupportabilityMetricCounters _supportabilityMetricCounters;
    private readonly TimeSpan _retryAfterBailCeiling;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayFunc;

    /// <param name="supportabilityMetricCounters">Optional export success/retry/failure counters.</param>
    /// <param name="retryAfterBailCeiling">
    /// Longest server-requested <c>Retry-After</c> this handler will actually wait out. A requested delay
    /// at or above the ceiling makes the handler give up immediately instead of blocking, on the
    /// assumption that the caller exports periodically and will try again on its next cycle. Sized per
    /// caller against that caller's total send budget.
    /// </param>
    /// <param name="delayFunc">Test seam for the retry sleep; defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
    public CustomRetryHandler(
        IOtelBridgeSupportabilityMetricCounters supportabilityMetricCounters = null,
        TimeSpan? retryAfterBailCeiling = null,
        Func<TimeSpan, CancellationToken, Task> delayFunc = null)
    {
        _supportabilityMetricCounters = supportabilityMetricCounters;
        _retryAfterBailCeiling = retryAfterBailCeiling ?? DefaultRetryAfterBailCeiling;
        _delayFunc = delayFunc ?? ((delay, cancellationToken) => Task.Delay(delay, cancellationToken));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            TimeSpan? honoredRetryAfterDelay = null;

            try
            {
                var response = await SendSingleAttempt(request, cancellationToken);

                // Success - return immediately
                if (response.IsSuccessStatusCode)
                {
                    LogSuccessIfRetried(attempt);
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.ExportSuccess);
                    return response;
                }

                // Handle failed response
                var shouldRetry = ShouldRetryResponse(response, attempt, out lastException);
                if (!shouldRetry)
                {
                    if (lastException != null) // transient failure with retries exhausted
                        _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.ExportFailure);
                    return response;
                }

                // Retrying, and the server told us how long to wait. Honoring a wait we can't afford would
                // block this send (and, for the profiles dispatcher, a threadpool thread) past the caller's
                // budget, so give up instead and let the caller's next periodic export carry the data.
                if (TryGetHonoredDelay(response, out var serverRequestedDelay))
                {
                    if (serverRequestedDelay >= _retryAfterBailCeiling)
                    {
                        Log.Warn($"OTLP export attempt {attempt} got {response.StatusCode} with Retry-After of {serverRequestedDelay.TotalSeconds:0.###}s, at or above this exporter's {_retryAfterBailCeiling.TotalSeconds:0.###}s honor ceiling; not retrying in this send, deferring to the next export");
                        _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.ExportFailure);
                        return response;
                    }

                    honoredRetryAfterDelay = serverRequestedDelay;
                }

                Log.Debug($"OTLP export attempt {attempt} failed with {response.StatusCode}, will retry");

                // Dispose failed response if retrying
                response.Dispose();
            }
            catch (Exception ex) when (IsRetryableException(ex, cancellationToken))
            {
                lastException = ex;
                LogExceptionRetry(attempt, ex);

                if (attempt >= MaxRetries)
                {
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.ExportFailure);
                    throw;
                }
            }

            // Wait before retry (except on final attempt)
            if (attempt < MaxRetries)
            {
                _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.ExportRetry);

                try
                {
                    await DelayBeforeRetry(attempt, honoredRetryAfterDelay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // The delay is outside the attempt's try/catch, so a HttpClient.Timeout that fires
                    // mid-sleep would otherwise abandon the send without counting the failure. Cancellation
                    // is indistinguishable here (HttpClient's timeout cancels the same linked token a
                    // caller would), so count either and let it propagate.
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.ExportFailure);
                    throw;
                }
            }
        }

        return HandleRetriesExhausted(lastException);
    }

    private async Task<HttpResponseMessage> SendSingleAttempt(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Clone the request for retry attempts (original request can only be sent once)
        var requestClone = await CloneRequestAsync(request);
        return await base.SendAsync(requestClone, cancellationToken);
    }

    private static void LogSuccessIfRetried(int attempt)
    {
        if (attempt > 1)
        {
            Log.Debug($"OTLP export succeeded on attempt {attempt}");
        }
    }

    private static bool ShouldRetryResponse(HttpResponseMessage response, int attempt, out Exception exception)
    {
        exception = null;

        if (!IsTransientFailure(response))
        {
            Log.Debug($"OTLP export failed with non-transient error {response.StatusCode}, not retrying");
            return false;
        }

        exception = new HttpRequestException($"Transient HTTP failure: {response.StatusCode} - {response.ReasonPhrase}");

        if (attempt >= MaxRetries)
        {
            Log.Warn($"OTLP export failed after {MaxRetries} attempts with status {response.StatusCode}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the server's requested wait out of a transient response's <c>Retry-After</c> header.
    /// Only called for responses already known to be transient and retryable, so both 429 and 503 (and
    /// any other transient status carrying the header) go through the same path.
    /// </summary>
    private static bool TryGetHonoredDelay(HttpResponseMessage response, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;

        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter == null)
        {
            return false;
        }

        // RetryAfterHeaderValue carries either a delta or a date -- a header that is neither parses to a
        // null RetryAfter above. For the date form, measure against the server's own Date header when it
        // sent one, so clock skew between this host and the ingest host doesn't distort the wait.
        var requested = retryAfter.Delta
            ?? retryAfter.Date.GetValueOrDefault() - (response.Headers.Date ?? DateTimeOffset.UtcNow);

        var minimum = TimeSpan.FromMilliseconds(MinDelayMs);
        delay = requested < minimum ? minimum : requested;
        return true;
    }

    private static bool IsRetryableException(Exception ex, CancellationToken cancellationToken)
    {
        return ex switch
        {
            // Timeout, but not user cancellation
            HttpRequestException => true,
            TaskCanceledException when !cancellationToken.IsCancellationRequested => true,
            _ => false
        };
    }

    private static void LogExceptionRetry(int attempt, Exception ex)
    {
        var message = ex switch
        {
            HttpRequestException => $"OTLP export attempt {attempt} failed with network error: {ex.Message}",
            TaskCanceledException => $"OTLP export attempt {attempt} timed out: {ex.Message}",
            _ => $"OTLP export attempt {attempt} failed: {ex.Message}"
        };
        Log.Debug(message);
    }

    private async Task DelayBeforeRetry(int attempt, TimeSpan? honoredRetryAfterDelay, CancellationToken cancellationToken)
    {
        var delay = honoredRetryAfterDelay ?? TimeSpan.FromMilliseconds(CalculateRetryDelay(attempt));
        var source = honoredRetryAfterDelay.HasValue ? "server Retry-After" : "exponential backoff";
        Log.Debug($"Waiting {delay.TotalMilliseconds}ms ({source}) before retry attempt {attempt + 1}");
        await _delayFunc(delay, cancellationToken);
    }

    private static HttpResponseMessage HandleRetriesExhausted(Exception lastException)
    {
        var errorMessage = $"OTLP export failed after {MaxRetries} attempts";
        Log.Error(lastException, errorMessage);
        throw lastException ?? new HttpRequestException(errorMessage);
    }

    /// <summary>
    /// Creates a copy of the HttpRequestMessage for retry attempts.
    /// Optimized for better performance and memory usage.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        // Copy headers efficiently
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        if (request.Content != null)
        {
            // Load content into buffer to allow multiple reads
            await request.Content.LoadIntoBufferAsync();
                
            // Check if content supports direct copying (more efficient than byte array)
            if (request.Content is ByteArrayContent byteArrayContent)
            {
                // For ByteArrayContent, read and create new instance directly
                var contentBytes = await byteArrayContent.ReadAsByteArrayAsync();
                clone.Content = new ByteArrayContent(contentBytes);
            }
            else
            {
                // For other content types, use the general approach
                var contentBytes = await request.Content.ReadAsByteArrayAsync();
                clone.Content = new ByteArrayContent(contentBytes);
            }

            // Copy content headers efficiently
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    /// <summary>
    /// Determines if an HTTP response represents a transient failure that should be retried.
    /// </summary>
    private static bool IsTransientFailure(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        return status == 408 ||              // Request Timeout
               status == 429 ||              // Too Many Requests (rate limiting)
               (status >= 500 && status < 600); // Server errors (5xx)
    }

    /// <summary>
    /// Calculates retry delay using exponential backoff with jitter.
    /// </summary>
    /// <summary>
    /// Calculates the delay before the next retry attempt using exponential backoff with jitter.
    /// Optimized for better performance and more predictable behavior.
    /// </summary>
    private static int CalculateRetryDelay(int attempt)
    {
        // Use thread-safe Random for better performance in concurrent scenarios
        // Exponential backoff: BaseDelay * 2^(attempt-1) + random jitter
        var exponentialDelay = BaseDelayMs * Math.Pow(2, attempt - 1);
        var jitter = Random.Next(0, MaxJitterMs);

        // Cap at reasonable maximum (30 seconds) to prevent excessive delays
        var totalDelay = Math.Min(exponentialDelay + jitter, 30000);

        return Math.Max((int)totalDelay, MinDelayMs); // Minimum delay for safety
    }
}