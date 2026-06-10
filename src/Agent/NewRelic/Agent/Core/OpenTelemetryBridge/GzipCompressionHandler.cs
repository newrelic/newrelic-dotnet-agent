// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.OpenTelemetryBridge;

/// <summary>
/// DelegatingHandler that gzip-compresses outgoing request content for OTLP exports.
/// The OpenTelemetry SDK version in use removed the built-in Compression option from
/// OtlpExporterOptions, so compression is applied here as the outermost handler in the
/// export pipeline. Placed outermost so compression runs once and any inner retry
/// handler resends the already-compressed payload.
/// </summary>
public class GzipCompressionHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content != null)
        {
            var originalBytes = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            using var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress, leaveOpen: true))
            {
                gzipStream.Write(originalBytes, 0, originalBytes.Length);
            }

            var compressedContent = new ByteArrayContent(compressedStream.ToArray());
            compressedContent.Headers.ContentType = request.Content.Headers.ContentType;
            compressedContent.Headers.ContentEncoding.Add("gzip");
            request.Content = compressedContent;
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
