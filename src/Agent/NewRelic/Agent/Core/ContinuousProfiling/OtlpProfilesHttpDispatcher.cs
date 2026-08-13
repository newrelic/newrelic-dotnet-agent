// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
using System.Net.Http.Headers;
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
/// has been removed, so this dispatch is invoked on every drain. The semantics are best-effort: any
/// failure is logged and dropped, returning <c>false</c>; it never throws and never retries.
///
/// HTTP infrastructure reuse: the proxy comes from the agent's <see cref="ConnectionInfo"/> (same
/// proxy config the collector wire uses) and the handler mirrors the
/// <c>NRHttpClient.GetHttpHandler</c> SocketsHttpHandler-with-fallback pattern. A dedicated
/// <see cref="HttpClient"/> is used rather than the collector's <c>IHttpClient</c> seam because the
/// latter hard-codes the collector's <c>invoke_raw_method</c> query-string URI scheme and is
/// unsuited to an absolute-URL OTLP POST.
/// </summary>
public class OtlpProfilesHttpDispatcher
{
    private const string ContentType = "application/x-protobuf";
    private const string ApiKeyHeader = "api-key";

    // CP profile sends are best-effort and never retried, so bound each send well below the collector's
    // default 120s timeout: DrainOnce invokes the send synchronously on the drain ThreadPool thread, so a
    // hung / blackholed OTLP endpoint must not park that thread for minutes. 15s is generous for a
    // slow-but-alive endpoint while capping the worst case.
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);

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
            // Best-effort: log and drop. A transport failure must never surface into the host, and we
            // deliberately do not retry -- a drained batch is disposable.
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

        var handler = new HttpClientHandler { Proxy = connectionInfo.Proxy };
        var httpClient = new HttpClient(handler, true) { Timeout = SendTimeout };

        return request => httpClient.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
