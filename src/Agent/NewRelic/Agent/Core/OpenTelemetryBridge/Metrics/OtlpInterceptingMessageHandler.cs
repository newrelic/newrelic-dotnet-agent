// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that intercepts outbound OTLP export HTTP requests,
/// captures the raw protobuf request body bytes, and returns a synthetic 200 OK response
/// without sending any real network traffic.
///
/// Used in serverless (Lambda) mode as the <see cref="HttpClient"/> transport for the OTel
/// SDK's OtlpMetricExporter. When <see cref="Drain"/> is called (triggered by
/// ForceFlush() at Lambda invocation end), the captured bytes are returned for inclusion
/// in the /tmp/newrelic-telemetry payload as "otlp_payload".
///
/// The Lambda extension reads this value, augments the OTLP resource (adding entity.guid
/// and other required metadata), and forwards the payload to the New Relic OTLP ingest
/// endpoint.
/// </summary>
public sealed class OtlpInterceptingMessageHandler : HttpMessageHandler
{
    private byte[] _capturedBytes;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content != null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            Interlocked.Exchange(ref _capturedBytes, bytes);
        }

        // Return a synthetic OK so the SDK considers the export successful.
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        };
    }

    /// <summary>
    /// Atomically retrieves and clears the last captured export bytes.
    /// Returns null if no export has occurred since the last drain.
    /// </summary>
    public byte[] Drain() => Interlocked.Exchange(ref _capturedBytes, null);
}
