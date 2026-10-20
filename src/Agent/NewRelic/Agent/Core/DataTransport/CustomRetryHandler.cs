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
/// <see cref="CustomRetryHandler(IExportRetrySupportabilityMetricCounters, TimeSpan?, Func{TimeSpan, CancellationToken, Task}, Func{DateTimeOffset}, bool, TimeSpan?)"/>.
///
/// Supportability counters: by default the handler records the terminal export outcome itself, since a
/// caller that reads the whole body up front (<c>HttpCompletionOption.ResponseContentRead</c>) is truly
/// done on a 2xx. A caller using <c>ResponseHeadersRead</c> -- e.g.
/// <see cref="NewRelic.Agent.Core.DataTransport.ContinuousProfiling.OtlpProfilesHttpDispatcher"/> -- can
/// still fail after a 2xx header (stalled/truncated body), so recording success here would lie; such
/// callers pass <c>recordTerminalOutcomes: false</c> and record their own confirmed outcome, while the
/// handler still records retries (the one outcome they can't observe themselves).
/// </summary>
public class CustomRetryHandler : DelegatingHandler
{
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 1000; // Start with 1 second
    private const int MaxJitterMs = 500;   // Max jitter of 500ms
    private const int MinDelayMs = 100;    // Floor for any retry delay, server-requested or computed
    private const int DefaultFailureLogSuppressionMinutes = 5; // Rate-limit window for failure Error logs

    // Per-thread Random for retry jitter. Two consumers (CP's drain, the metrics exporter's periodic
    // read) can each invoke SendAsync concurrently on different threads; System.Random is not
    // thread-safe, so each thread gets its own instance instead of sharing one.
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

    private static readonly TimeSpan DefaultRetryAfterBailCeiling = TimeSpan.FromSeconds(5);

    private readonly IExportRetrySupportabilityMetricCounters _supportabilityMetricCounters;
    private readonly TimeSpan _retryAfterBailCeiling;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayFunc;
    private readonly Func<DateTimeOffset> _utcNowFunc;
    private readonly bool _recordTerminalOutcomes;

    // Rate-limit state for failure Error logs. A persistent non-transient status (401/403/404/400/413) or a
    // persistent network failure (misconfigured endpoint, air-gapped host) is not something a retry can fix,
    // so without this the handler would log one Error per export interval forever. Instead: log the FIRST
    // failure at Error, then at most one Error per _failureLogSuppressionWindow, folding the suppressed count
    // into the next Error; suppressed failures drop to Debug. A success resets the throttle so a later, new
    // outage logs at Error again. Supportability counters (RecordExportFailure) stay per-failure -- only the
    // logging is rate-limited. Guarded by _failureLogLock since the metrics exporter and the CP drain each
    // drive their own handler instance, but a single instance may still see concurrent sends.
    private readonly TimeSpan _failureLogSuppressionWindow;
    private readonly object _failureLogLock = new object();
    private DateTimeOffset? _lastFailureLogTime;
    private int _suppressedFailureCount;

    /// <param name="supportabilityMetricCounters">Optional export success/retry/failure counters.</param>
    /// <param name="retryAfterBailCeiling">
    /// Longest server-requested <c>Retry-After</c> this handler will actually wait out. A requested delay
    /// at or above the ceiling is ignored -- instead of blocking the caller's thread for that long, the
    /// handler falls back to its own computed exponential backoff (<see cref="CalculateRetryDelay"/>) and
    /// retries on the usual schedule; it does not give up or stop retrying. Sized per caller against that
    /// caller's total send budget.
    /// </param>
    /// <param name="delayFunc">Test seam for the retry sleep; defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
    /// <param name="utcNowFunc">
    /// Test seam for the "now" used to compute a Retry-After date's delay when the response carries no
    /// Date header (see <see cref="TryGetHonoredDelay"/>); defaults to <see cref="DateTimeOffset.UtcNow"/>.
    /// </param>
    /// <param name="recordTerminalOutcomes">
    /// When <c>true</c> (default) the handler records the terminal export success/failure counters itself.
    /// When <c>false</c> it records only retries and leaves success/failure to the caller -- for a caller
    /// that completes on response headers and confirms acceptance only after its own body read (see the
    /// class summary). Retries are always recorded regardless, since the caller cannot observe them from
    /// the final response or exception.
    /// </param>
    /// <param name="failureLogSuppressionWindow">
    /// Rate-limit window for failure Error logs (defaults to <see cref="DefaultFailureLogSuppressionMinutes"/>
    /// minutes). The first failure logs at Error; subsequent failures within the window drop to Debug and are
    /// counted; the next Error past the window includes the suppressed count. A success resets this. The
    /// <paramref name="utcNowFunc"/> clock drives the window, so it is injectable for tests.
    /// </param>
    public CustomRetryHandler(
        IExportRetrySupportabilityMetricCounters supportabilityMetricCounters = null,
        TimeSpan? retryAfterBailCeiling = null,
        Func<TimeSpan, CancellationToken, Task> delayFunc = null,
        Func<DateTimeOffset> utcNowFunc = null,
        bool recordTerminalOutcomes = true,
        TimeSpan? failureLogSuppressionWindow = null)
    {
        _supportabilityMetricCounters = supportabilityMetricCounters;
        _retryAfterBailCeiling = retryAfterBailCeiling ?? DefaultRetryAfterBailCeiling;
        _delayFunc = delayFunc ?? ((delay, cancellationToken) => Task.Delay(delay, cancellationToken));
        _utcNowFunc = utcNowFunc ?? (() => DateTimeOffset.UtcNow);
        _recordTerminalOutcomes = recordTerminalOutcomes;
        _failureLogSuppressionWindow = failureLogSuppressionWindow ?? TimeSpan.FromMinutes(DefaultFailureLogSuppressionMinutes);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Exception lastException = null;

        // Read the body once for the whole retry loop instead of re-reading it on every clone/attempt --
        // this handler is also driven with multi-hundred-KB-to-multi-MB Continuous Profiling payloads, so
        // a per-attempt re-read is not free. The bytes are read-only after this point and shared by
        // reference across each attempt's ByteArrayContent; no attempt ever writes back into the array.
        var contentBytes = request.Content != null ? await request.Content.ReadAsByteArrayAsync() : null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            TimeSpan? honoredRetryAfterDelay = null;

            using var requestClone = CloneRequest(request, contentBytes);

            try
            {
                var response = await base.SendAsync(requestClone, cancellationToken);

                // Success - return immediately
                if (response.IsSuccessStatusCode)
                {
                    LogSuccessIfRetried(attempt);
                    ResetFailureLogThrottle();
                    if (_recordTerminalOutcomes)
                        _supportabilityMetricCounters?.RecordExportSuccess();
                    return response;
                }

                // Handle failed response
                var shouldRetry = ShouldRetryResponse(response, attempt, out lastException);
                if (!shouldRetry)
                {
                    // Terminal non-2xx: either a non-transient rejection (401/403/404/400/413) or a
                    // transient status whose retries are exhausted. Both are send failures. The Error log
                    // is rate-limited (see LogExportFailure) so a persistently-misconfigured endpoint
                    // surfaces one Error, then stays quiet, instead of one Error per export interval.
                    LogExportFailure(lastException, $"OTLP export failed with status {(int)response.StatusCode} ({response.StatusCode}); not retrying");
                    if (_recordTerminalOutcomes)
                        _supportabilityMetricCounters?.RecordExportFailure();
                    return response;
                }

                // Retrying, and the server told us how long to wait. Honoring a wait we can't afford would
                // block this send (and, for the profiles dispatcher, a threadpool thread) past the caller's
                // budget, so ignore the server's requested delay and fall back to the computed exponential
                // backoff instead of abandoning the retry loop entirely.
                if (TryGetHonoredDelay(response, out var serverRequestedDelay))
                {
                    if (serverRequestedDelay >= _retryAfterBailCeiling)
                    {
                        Log.Warn($"OTLP export attempt {attempt} got {response.StatusCode} with Retry-After of {serverRequestedDelay.TotalSeconds:0.###}s, at or above this exporter's {_retryAfterBailCeiling.TotalSeconds:0.###}s honor ceiling; ignoring Retry-After and using exponential backoff instead");
                    }
                    else
                    {
                        honoredRetryAfterDelay = serverRequestedDelay;
                    }
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
                    LogExportFailure(ex, $"OTLP export failed after {MaxRetries} attempts");
                    if (_recordTerminalOutcomes)
                        _supportabilityMetricCounters?.RecordExportFailure();
                    throw;
                }
            }
            catch (Exception ex)
            {
                // Not retryable, so this is a terminal failure for this send -- notably including a
                // send-time TaskCanceledException that looks like user cancellation (IsRetryableException
                // returned false above). That's the same ambiguous HttpClient.Timeout-vs-cancellation case
                // the mid-backoff catch below already can't distinguish, so count it the same way: either
                // outcome is a real failure, and this is the only path an exception here can take.
                LogExportFailure(ex, "OTLP export failed with a non-retryable error");
                if (_recordTerminalOutcomes)
                    _supportabilityMetricCounters?.RecordExportFailure();
                throw;
            }

            // Wait before retry (except on final attempt)
            if (attempt < MaxRetries)
            {
                _supportabilityMetricCounters?.RecordExportRetry();

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
                    if (_recordTerminalOutcomes)
                        _supportabilityMetricCounters?.RecordExportFailure();
                    throw;
                }
            }
        }

        // Every iteration above either returns or throws once attempt reaches MaxRetries, so the loop
        // never falls out the bottom -- this satisfies the compiler's control-flow requirement only.
        throw new InvalidOperationException("Unreachable: CustomRetryHandler retry loop always returns or throws before exhausting its iterations.");
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
            // Detail only -- the terminal caller logs the (rate-limited) Error for this exhausted failure.
            Log.Debug($"OTLP export failed after {MaxRetries} attempts with status {response.StatusCode}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the server's requested wait out of a transient response's <c>Retry-After</c> header.
    /// Only called for responses already known to be transient and retryable, so both 429 and 503 (and
    /// any other transient status carrying the header) go through the same path.
    /// </summary>
    private bool TryGetHonoredDelay(HttpResponseMessage response, out TimeSpan delay)
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
            ?? retryAfter.Date.GetValueOrDefault() - (response.Headers.Date ?? _utcNowFunc());

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

    /// <summary>
    /// Logs a terminal export failure, rate-limited so a persistent failure (misconfigured endpoint,
    /// air-gapped host, a non-transient 4xx) does not emit one Error per export interval forever. The first
    /// failure in a streak logs at Error; subsequent failures within <see cref="_failureLogSuppressionWindow"/>
    /// drop to Debug and are counted; the next Error past the window folds in the suppressed count. The
    /// throttle is reset by a successful export (see <see cref="ResetFailureLogThrottle"/>), so a later, new
    /// outage logs its first failure at Error again. Supportability failure counters are recorded by the
    /// caller on every failure regardless -- only the logging is rate-limited.
    /// </summary>
    private void LogExportFailure(Exception exception, string message)
    {
        bool logAtError;
        var suppressedCount = 0;

        lock (_failureLogLock)
        {
            var now = _utcNowFunc();
            if (_lastFailureLogTime == null || (now - _lastFailureLogTime.Value) >= _failureLogSuppressionWindow)
            {
                logAtError = true;
                suppressedCount = _suppressedFailureCount;
                _suppressedFailureCount = 0;
                _lastFailureLogTime = now;
            }
            else
            {
                logAtError = false;
                _suppressedFailureCount++;
            }
        }

        if (!logAtError)
        {
            if (exception != null)
                Log.Debug(exception, message);
            else
                Log.Debug(message);
            return;
        }

        var errorMessage = suppressedCount > 0
            ? $"{message} ({suppressedCount} further export failure(s) suppressed since the last error was logged)"
            : message;

        if (exception != null)
            Log.Error(exception, errorMessage);
        else
            Log.Error(errorMessage);
    }

    private void ResetFailureLogThrottle()
    {
        lock (_failureLogLock)
        {
            // Clear the window so a later, unrelated outage logs its first failure at Error rather than being
            // silently rate-limited by a window that opened during the previous (now-recovered) outage.
            _lastFailureLogTime = null;
            _suppressedFailureCount = 0;
        }
    }

    private async Task DelayBeforeRetry(int attempt, TimeSpan? honoredRetryAfterDelay, CancellationToken cancellationToken)
    {
        var delay = honoredRetryAfterDelay ?? TimeSpan.FromMilliseconds(CalculateRetryDelay(attempt));
        var source = honoredRetryAfterDelay.HasValue ? "server Retry-After" : "exponential backoff";
        Log.Debug($"Waiting {delay.TotalMilliseconds}ms ({source}) before retry attempt {attempt + 1}");
        await _delayFunc(delay, cancellationToken);
    }

    /// <summary>
    /// Creates a copy of the HttpRequestMessage for a single attempt. <paramref name="contentBytes"/> is
    /// read once for the whole retry loop and wrapped in a new <see cref="ByteArrayContent"/> per clone --
    /// wrapping does not copy the array, so each attempt costs one small allocation instead of a full
    /// buffer-then-read-then-copy round trip.
    /// </summary>
    private static HttpRequestMessage CloneRequest(HttpRequestMessage request, byte[] contentBytes)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (contentBytes != null)
        {
            clone.Content = new ByteArrayContent(contentBytes);

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
    /// Calculates the delay before the next retry attempt using exponential backoff with jitter.
    /// </summary>
    private static int CalculateRetryDelay(int attempt)
    {
        // Exponential backoff: BaseDelay * 2^(attempt-1) + random jitter
        var exponentialDelay = BaseDelayMs * Math.Pow(2, attempt - 1);
        var jitter = ThreadLocalRandom.Value.Next(0, MaxJitterMs);

        // Cap at a reasonable maximum (30 seconds) to prevent excessive delays. Unreachable today --
        // attempt is bounded by MaxRetries (3), whose worst case (BaseDelayMs * 2^2 + MaxJitterMs =
        // 4500ms) never approaches the cap -- so this is currently untested dead code. It only starts
        // mattering, and becomes testable, the moment MaxRetries grows enough for the exponential term
        // to reach 30000ms (attempt 6: 1000 * 2^5 = 32000ms). Left in place as a deliberate safety net
        // against that future change rather than removed as dead code.
        var totalDelay = Math.Min(exponentialDelay + jitter, 30000);

        return Math.Max((int)totalDelay, MinDelayMs); // Minimum delay for safety
    }
}