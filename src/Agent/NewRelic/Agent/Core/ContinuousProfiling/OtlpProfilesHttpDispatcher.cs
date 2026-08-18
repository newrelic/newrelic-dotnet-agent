// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// The real OTLP/HTTP protobuf dispatch for continuous-profiling. Builds and POSTs a serialized
/// <see cref="OpenTelemetry.Proto.Collector.Profiles.V1Development.ExportProfilesServiceRequest"/>
/// to the resolved profiles endpoint with <c>Content-Type: application/x-protobuf</c> and the
/// <c>api-key</c> (license key) header. Entity association (service.name / resource attributes) is
/// already stamped on the request body by <see cref="OtlpProfileBuilder"/>.
///
/// It is wired as the <c>httpPost</c> delegate of <see cref="ProfilesTransport"/>, whose no-send guard
/// has been removed, so this dispatch is invoked on every drain. The semantics are best-effort: the real
/// send path retries transient failures a bounded number of times via
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

    // Per-attempt connect bound (SocketsHttpHandler.ConnectTimeout). Renamed from the old SendTimeout:
    // this now bounds only ONE attempt's TCP connect, not the whole multi-attempt send -- see
    // TotalSendTimeoutWithRetries for the budget that covers the full CustomRetryHandler sequence.
    public static readonly TimeSpan AttemptConnectTimeout = TimeSpan.FromSeconds(15);

    // HttpClient.Timeout bounds the ENTIRE SendAsync call, including every retry attempt and every
    // inter-attempt backoff delay CustomRetryHandler injects -- not just one attempt. Sized for
    // CustomRetryHandler's MaxRetries=3 * AttemptConnectTimeout worst case, plus its own backoff/jitter
    // (~1s + ~2s across the two inter-attempt gaps, capped well below 30s each in practice), rounded up
    // with margin. Kept comfortably under ContinuousProfilingService.DrainShutdownWaitTimeout (60s) so
    // that bounded wait always covers a send that is legitimately still retrying, not just one that's hung.
    public static readonly TimeSpan TotalSendTimeoutWithRetries = TimeSpan.FromSeconds(45);

    private readonly IConfiguration _configuration;
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

    public OtlpProfilesHttpDispatcher(IConfiguration configuration)
        : this(configuration, null)
    {
    }

    // The send delegate is injected for testability. When null, a lazily-created HttpClient over the
    // agent's proxy configuration performs the real network send (the one branch we do not exercise
    // in unit tests -- see CreateRealSend).
    public OtlpProfilesHttpDispatcher(IConfiguration configuration, Func<HttpRequestMessage, HttpResponseMessage> send)
    {
        _configuration = configuration;
        _send = send ?? CreateRealSend(configuration);
    }

    /// <summary>
    /// Best-effort POST of the serialized request to <paramref name="endpoint"/>. Returns a
    /// <see cref="ProfilesSendResult"/> (accepted flag, HTTP status, response body) so the caller can log
    /// the send like the collector wire. Never throws; a failure is reported as <c>(false, 0, "")</c>.
    /// </summary>
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
            using var response = _send(request);
            if (response == null)
                return new ProfilesSendResult(false, 0, string.Empty);

            var content = response.Content?.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult() ?? string.Empty;
            return new ProfilesSendResult(response.IsSuccessStatusCode, (int)response.StatusCode, content);
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

        var content = new ByteArrayContent(payload ?? Array.Empty<byte>());
        content.Headers.ContentType = new MediaTypeHeaderValue(ContentType);
        request.Content = content;

        return request;
    }

    // Not exercised by unit tests: this constructs a live HttpClient and performs a real network send.
    // The transport-failure and response-handling logic is tested via an injected send delegate.
    [NrExcludeFromCodeCoverage]
    private static Func<HttpRequestMessage, HttpResponseMessage> CreateRealSend(IConfiguration configuration)
    {
        var connectionInfo = new ConnectionInfo(configuration);

        var innerHandler = CreateHandler(connectionInfo.Proxy);
        var retryHandler = new CustomRetryHandler { InnerHandler = innerHandler };
        var httpClient = new HttpClient(retryHandler, true) { Timeout = TotalSendTimeoutWithRetries };

        return request => httpClient.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
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
