// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;

namespace NewRelic.Agent.Core.DataTransport.ContinuousProfiling;

/// <summary>
/// The real OTLP/HTTP protobuf dispatch for continuous-profiling. Builds and POSTs a serialized
/// <see cref="OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest"/>
/// to the resolved profiles endpoint with <c>Content-Type: application/x-protobuf</c> and the
/// <c>api-key</c> (license key) header. Entity association (service.name / resource attributes) is
/// already stamped on the request body by <see cref="NewRelic.Agent.Core.ContinuousProfiling.OtlpProfileBuilder"/>.
///
/// Best-effort: the real send path retries transient failures a bounded number of times via
/// <see cref="NewRelic.Agent.Core.DataTransport.CustomRetryHandler"/>, but once that budget is exhausted
/// (or for a non-retryable outcome) the failure is logged and the batch is dropped, returning
/// <c>false</c>; it never throws.
///
/// HTTP infrastructure reuse: the proxy comes from the agent's <see cref="ConnectionInfo"/> (same
/// proxy config the collector wire uses) and the handler mirrors the
/// <c>NRHttpClient.GetHttpHandler</c> SocketsHttpHandler-with-fallback pattern, including its bounded
/// pooled-connection lifetime so connections recycle across ingest DNS changes. A dedicated
/// <see cref="HttpClient"/> is used rather than the collector's <c>IHttpClient</c> seam because the
/// latter hard-codes the collector's <c>invoke_raw_method</c> query-string URI scheme and is
/// unsuited to an absolute-URL OTLP POST.
/// </summary>
public class OtlpProfilesHttpDispatcher
{
    private const string ContentType = "application/x-protobuf";
    private const string ApiKeyHeader = "api-key";
    private const string UserAgentHeader = "User-Agent";

    // Per-attempt connect bound (SocketsHttpHandler.ConnectTimeout) -- bounds only ONE attempt's TCP
    // connect. See TotalSendTimeoutWithRetries for the budget covering the full retry sequence.
    public static readonly TimeSpan AttemptConnectTimeout = TimeSpan.FromSeconds(15);

    // HttpClient.Timeout bounds every retry attempt and inter-attempt backoff CustomRetryHandler injects,
    // through headers-only completion (CreateRealSend uses ResponseHeadersRead) -- NOT the body read,
    // which has its own deadline, BodyReadTimeout. The two together (45s + 10s = 55s) stay under
    // ContinuousProfilingService.DrainShutdownWaitTimeout (60s).
    public static readonly TimeSpan TotalSendTimeoutWithRetries = TimeSpan.FromSeconds(45);

    // Deadline for reading the response body once headers have arrived (see ReadResponseBodyBounded).
    public static readonly TimeSpan BodyReadTimeout = TimeSpan.FromSeconds(10);

    // Cap on how much of the response body is ever read into memory. Diagnostics-only data (a real OTLP
    // partial-success ack is a few hundred bytes; a proxy/collector error page is at most a few KB) --
    // this is generous headroom for that, not a real budget for an adversarial/misbehaving response.
    public const int MaxResponseBodyBytes = 64 * 1024;

    private readonly IConfiguration _configuration;

    // Lazy: this dispatcher is constructed by ContinuousProfilingServiceFactory in every
    // non-serverless process, including every one that never enables continuous profiling (the
    // feature defaults off). The real pipeline (ConnectionInfo -> reflected SocketsHttpHandler ->
    // CustomRetryHandler -> a process-lifetime HttpClient) is pure cost until a profile is actually
    // shipped, so build it on first use rather than in the constructor. Lazy<T>'s default
    // ExecutionAndPublication mode means two racing sends still build it exactly once.
    private readonly Lazy<Func<HttpRequestMessage, HttpResponseMessage>> _send;

    public OtlpProfilesHttpDispatcher(IConfiguration configuration, IExportRetrySupportabilityMetricCounters supportabilityMetricCounters = null)
        : this(configuration, (Func<HttpRequestMessage, HttpResponseMessage>)null, supportabilityMetricCounters)
    {
    }

    // The send delegate is injected for testability. When null, the real network send (a
    // lazily-created HttpClient over the agent's proxy configuration) is built on first Post -- the
    // one branch we do not exercise in unit tests -- see CreateRealSend.
    public OtlpProfilesHttpDispatcher(IConfiguration configuration, Func<HttpRequestMessage, HttpResponseMessage> send)
        : this(configuration, send, null)
    {
    }

    // Test seam: builds the real CustomRetryHandler + HttpClient pipeline (the same code
    // CreateRealSend uses) over a caller-supplied inner handler, so unit tests can exercise
    // retry/Retry-After/counters wiring end to end without a socket. delayFunc threads through to
    // CustomRetryHandler's own test seam so retry-backoff tests don't have to sleep for real.
    public OtlpProfilesHttpDispatcher(IConfiguration configuration, HttpMessageHandler innerHandler, IExportRetrySupportabilityMetricCounters supportabilityMetricCounters = null, Func<TimeSpan, CancellationToken, Task> delayFunc = null)
        : this(configuration, BuildSend(innerHandler, supportabilityMetricCounters, delayFunc), null)
    {
    }

    private OtlpProfilesHttpDispatcher(IConfiguration configuration, Func<HttpRequestMessage, HttpResponseMessage> send, IExportRetrySupportabilityMetricCounters supportabilityMetricCounters)
    {
        _configuration = configuration;
        _send = send != null
            ? new Lazy<Func<HttpRequestMessage, HttpResponseMessage>>(() => send)
            : new Lazy<Func<HttpRequestMessage, HttpResponseMessage>>(() => CreateRealSend(configuration, supportabilityMetricCounters));
    }

    /// <summary>
    /// Best-effort POST of the serialized request to <paramref name="endpoint"/>. Returns a
    /// <see cref="ProfilesSendResult"/> (accepted flag, HTTP status, response body) so the caller can log
    /// the send like the collector wire. Never throws; a failure is reported as <c>(false, 0, "")</c>.
    /// </summary>
    /// <remarks>
    /// No payload-size guard before POST: <see cref="NewRelic.Agent.Core.ContinuousProfiling.ContinuousProfilingService"/>'s
    /// fixed drain buffer already bounds a batch, and the request is gzip-compressed here regardless of
    /// size. Deliberately skipped rather than deferred -- revisit only if a real oversized-batch failure
    /// shows up.
    /// </remarks>
    public ProfilesSendResult Post(byte[] payload, string endpoint)
    {
        try
        {
            if (string.IsNullOrEmpty(endpoint) || !Uri.IsWellFormedUriString(endpoint, UriKind.Absolute))
            {
                Log.Debug("[ContinuousProfiling] Not dispatching: endpoint '{0}' is not a valid absolute URI.", endpoint);
                return new ProfilesSendResult(false, 0, string.Empty);
            }

            using var request = BuildRequestMessage(payload, endpoint);
            using var response = _send.Value(request);
            if (response == null)
                return new ProfilesSendResult(false, 0, string.Empty);

            var (contentBytes, truncated) = ReadResponseBodyBounded(response.Content);
            var content = Encoding.UTF8.GetString(contentBytes);
            if (truncated)
                Log.Debug("[ContinuousProfiling] Response body from {0} exceeded {1} bytes; truncated for logging.", endpoint, MaxResponseBodyBytes);

            // A truncated body can decode a protobuf message at a byte boundary that happens to look
            // valid, so a bogus RejectedProfiles/error message could surface -- skip the parse entirely.
            var (rejectedProfiles, partialSuccessErrorMessage) = truncated
                ? (0, string.Empty)
                : TryParsePartialSuccess(response, contentBytes);

            return new ProfilesSendResult(response.IsSuccessStatusCode, (int)response.StatusCode, content, rejectedProfiles, partialSuccessErrorMessage);
        }
        catch (Exception ex)
        {
            // Best-effort: log and drop. A transport failure must never surface into the host. Transient
            // failures already got a bounded retry via CustomRetryHandler (see the class doc); once that
            // budget is exhausted, or for a non-retryable outcome, the batch is disposable -- there is no
            // held-over-cycle recovery like a harvest, so give up and let the next drain try fresh.
            Log.Debug(ex, "[ContinuousProfiling] Profiles POST to {0} failed; dropping the batch.", endpoint);
            return new ProfilesSendResult(false, 0, string.Empty);
        }
    }

    /// <summary>
    /// Diagnostics only -- never affects <see cref="ProfilesSendResult.Accepted"/>. Only attempts the
    /// protobuf parse when the response declares protobuf content (skips proxy/HTML error pages); never
    /// throws.
    /// </summary>
    private static (long rejectedProfiles, string errorMessage) TryParsePartialSuccess(HttpResponseMessage response, byte[] contentBytes)
    {
        var mediaType = response.Content?.Headers?.ContentType?.MediaType;
        if (contentBytes.Length == 0 || !string.Equals(mediaType, ContentType, StringComparison.OrdinalIgnoreCase))
            return (0, string.Empty);

        try
        {
            var parsed = ExportProfilesServiceResponse.Parser.ParseFrom(contentBytes);
            var partialSuccess = parsed.PartialSuccess;
            return partialSuccess == null ? (0, string.Empty) : (partialSuccess.RejectedProfiles, partialSuccess.ErrorMessage ?? string.Empty);
        }
        catch (InvalidProtocolBufferException ex)
        {
            Log.Finest(ex, "[ContinuousProfiling] Could not parse ExportProfilesServiceResponse; skipping partial-success diagnostics.");
            return (0, string.Empty);
        }
    }

    /// <summary>
    /// Reads the response body into memory, capped at <see cref="MaxResponseBodyBytes"/> regardless of
    /// what <c>Content-Length</c> claims -- covers chunked/absent-length responses too. Bounded by
    /// <see cref="BodyReadTimeout"/>; a stalled body throws <see cref="OperationCanceledException"/>.
    /// Once the cap is hit (declared or actual), the rest of the stream is still drained to EOF rather
    /// than abandoned -- an HttpClient response disposed with unread bytes still on the wire can prevent
    /// the underlying connection from being returned to the pool cleanly, so draining protects reuse of
    /// the process-lifetime <see cref="HttpClient"/> this dispatcher shares across every drain.
    /// </summary>
    private static (byte[] bytes, bool truncated) ReadResponseBodyBounded(HttpContent content)
    {
        if (content == null)
            return (Array.Empty<byte>(), false);

        using var cts = new CancellationTokenSource(BodyReadTimeout);
        using var stream = content.ReadAsStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        var declaredLength = content.Headers?.ContentLength;
        if (declaredLength.HasValue && declaredLength.Value > MaxResponseBodyBytes)
        {
            DrainStream(stream, cts.Token);
            return (Array.Empty<byte>(), true);
        }

        using var buffered = new MemoryStream();

        var buffer = new byte[8192];
        int read;
        var truncated = false;
        while ((read = stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false).GetAwaiter().GetResult()) > 0)
        {
            if (truncated)
                continue;

            var remaining = MaxResponseBodyBytes - (int)buffered.Length;
            var toCopy = Math.Min(read, remaining);
            buffered.Write(buffer, 0, toCopy);

            if (toCopy < read || buffered.Length >= MaxResponseBodyBytes)
                truncated = true;
        }

        return (buffered.ToArray(), truncated);
    }

    // Reads to EOF and discards, so a body that was capped (declared oversized, or hit the actual cap
    // mid-read) is still fully consumed -- see ReadResponseBodyBounded's remarks on why.
    private static void DrainStream(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult() > 0)
        {
        }
    }

    /// <summary>
    /// Builds the OTLP/HTTP request message (absolute endpoint URI, protobuf content type, api-key
    /// header, serialized body). Factored out so the request shape is unit-testable without a socket.
    /// </summary>
    public HttpRequestMessage BuildRequestMessage(byte[] payload, string endpoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint));

        var licenseKey = _configuration?.AgentLicenseKey;
        if (!string.IsNullOrEmpty(licenseKey))
        {
            request.Headers.TryAddWithoutValidation(ApiKeyHeader, licenseKey);
        }

        // Matches the format OtlpExporterConfigurationService uses for the metrics OTLP export path.
        request.Headers.TryAddWithoutValidation(UserAgentHeader, $"NewRelic-DotNet-Agent/{AgentInstallConfiguration.AgentVersion ?? "Unknown"}");

        var body = Gzip(payload ?? Array.Empty<byte>());
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(ContentType);
        content.Headers.ContentEncoding.Add("gzip");
        request.Content = content;

        return request;
    }

    // otlp-ingest (the collector's OTLP HTTP entry point) accepts gzip/zstd/identity via the standard
    // Content-Encoding header for every OTLP signal (traces/metrics/profiles alike) -- see
    // OtlpDeserializer.extractHttpPayload. CP profile batches are text-heavy (repeated stack frame/thread
    // names), so gzip meaningfully shrinks the wire payload; there is exactly one POST per drain (no
    // splitting -- see ContinuousProfilingService.DrainOnce), so compressing once here covers the whole send.
    private static byte[] Gzip(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(payload, 0, payload.Length);
        }
        return output.ToArray();
    }

    // Not exercised by unit tests: this constructs a live HttpClient over a real socket handler. The
    // retry/timeout/counters wiring it builds on top of (BuildSend) is exercised directly -- see the
    // HttpMessageHandler-injecting ctor above.
    [NrExcludeFromCodeCoverage]
    private static Func<HttpRequestMessage, HttpResponseMessage> CreateRealSend(IConfiguration configuration, IExportRetrySupportabilityMetricCounters supportabilityMetricCounters)
    {
        var connectionInfo = new ConnectionInfo(configuration);
        var innerHandler = CreateHandler(connectionInfo.Proxy);
        return BuildSend(innerHandler, supportabilityMetricCounters, null);
    }

    // Builds the retry-handler + HttpClient chain over the given inner transport handler. Shared by
    // the real network path (CreateRealSend) and the test seam ctor, so both exercise identical wiring.
    private static Func<HttpRequestMessage, HttpResponseMessage> BuildSend(HttpMessageHandler innerHandler, IExportRetrySupportabilityMetricCounters supportabilityMetricCounters, Func<TimeSpan, CancellationToken, Task> delayFunc)
    {
        // 5s keeps any single honored Retry-After small against both the TotalSendTimeoutWithRetries
        // budget this client is given and the 1-60s drain cadence of the service driving it; a longer
        // server-requested wait is declined so the send doesn't hold its threadpool thread through a
        // DrainBufferBoundary or extend the bounded drain-wait on shutdown.
        var retryHandler = new CustomRetryHandler(supportabilityMetricCounters, retryAfterBailCeiling: TimeSpan.FromSeconds(5), delayFunc: delayFunc) { InnerHandler = innerHandler };
        var httpClient = new HttpClient(retryHandler, true) { Timeout = TotalSendTimeoutWithRetries };

        // ResponseHeadersRead: HttpClient must not implicitly buffer the whole body into memory before
        // returning -- ReadResponseBodyBounded reads (and caps) it explicitly instead. See
        // TotalSendTimeoutWithRetries/BodyReadTimeout for why this changes what HttpClient.Timeout covers.
        return request => httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    // Mirrors NRHttpClient.GetHttpHandler. This client lives for the process lifetime, and
    // HttpClientHandler's default PooledConnectionLifetime is infinite, so a pooled connection would
    // survive the ingest host's resolved IP rotating. Core targets net462/netstandard2.0, where
    // SocketsHttpHandler does not exist at compile time -- hence the runtime version check and
    // reflection, matching NRHttpClient rather than a TFM conditional.
    [NrExcludeFromCodeCoverage]
    private static HttpMessageHandler CreateHandler(IWebProxy proxy)
    {
        if (System.Environment.Version.Major >= 6)
        {
            try
            {
                var pooledConnectionLifetime = TimeSpan.FromMinutes(5); // an in-use connection will be closed and recycled after 5 minutes
                var pooledConnectionIdleTimeout = TimeSpan.FromMinutes(1); // a connection that is idle for 1 minute will be closed and recycled

                var assembly = Assembly.Load("System.Net.Http");
                var handlerType = assembly.GetType("System.Net.Http.SocketsHttpHandler");
                dynamic handler = Activator.CreateInstance(handlerType);

                handler.PooledConnectionLifetime = pooledConnectionLifetime;
                handler.PooledConnectionIdleTimeout = pooledConnectionIdleTimeout;
                handler.ConnectTimeout = AttemptConnectTimeout;
                handler.Proxy = proxy;

                Log.Debug("[ContinuousProfiling] Created a SocketsHttpHandler with PooledConnectionLifetime {0}, PooledConnectionIdleTimeout {1} and ConnectTimeout {2}.",
                    pooledConnectionLifetime, pooledConnectionIdleTimeout, AttemptConnectTimeout);

                return (HttpMessageHandler)handler;
            }
            catch (Exception e)
            {
                Log.Debug(e, "[ContinuousProfiling] Application runtime is .NET 6+ but an exception occurred trying to create SocketsHttpHandler. Falling back to HttpClientHandler.");
            }
        }

        return new HttpClientHandler { Proxy = proxy };
    }
}
