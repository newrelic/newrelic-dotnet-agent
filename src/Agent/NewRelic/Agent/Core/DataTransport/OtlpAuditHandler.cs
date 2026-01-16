// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport.Client;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport
{
    /// <summary>
    /// DelegatingHandler that provides audit logging for OTLP (OpenTelemetry Protocol) HTTP communications.
    /// Captures requests and responses for compliance auditing while handling binary protobuf content.
    /// </summary>
    public class OtlpAuditHandler : DelegatingHandler
    {
        /// <summary>
        /// Sends an HTTP request and logs request/response data for audit purposes.
        /// </summary>
        /// <param name="request">The HTTP request message to send</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>The HTTP response message</returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Log.Finest("Exporting OTLP metrics to {RequestUri}", request.RequestUri);
            
            // Audit log the outgoing request - await to ensure completion
            await LogOtlpRequest(request).ConfigureAwait(false);

            HttpResponseMessage response = null;
            try
            {
                // Send the request through the pipeline
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                // Audit log the response - await to ensure completion
                await LogOtlpResponse(response).ConfigureAwait(false);

                Log.Debug("Exported OTLP metrics to {RequestUri} with status {ResponseStatusCode}", request.RequestUri, response.StatusCode);
                return response;
            }
            catch (Exception ex)
            {
                // Log HTTP-level error at finest level for debugging
                Log.Finest(ex, "OTLP metrics export failed: {ExceptionType} - {Message}. Endpoint: {RequestUri}, Method: {Method}", 
                    ex.GetType().Name, ex.Message, request.RequestUri, request.Method);
                
                // Exception will propagate to OpenTelemetry SDK, which will log its own error
                // via EventSource that our OpenTelemetrySDKLogger captures
                throw;
            }
        }

        private static async Task LogOtlpRequest(HttpRequestMessage request)
        {
            try
            {
                // Log the request URI (with license key obfuscation)
                DataTransportAuditLogger.Log(
                    DataTransportAuditLogger.AuditLogDirection.Sent,
                    DataTransportAuditLogger.AuditLogSource.InstrumentedApp,
                    request.RequestUri?.ToString() ?? "Unknown URI");

                // Log request content - for binary protobuf, encode as base64 for audit purposes
                if (request.Content != null)
                {
                    var contentData = await GetContentForAuditLog(request.Content).ConfigureAwait(false);
                    DataTransportAuditLogger.Log(
                        DataTransportAuditLogger.AuditLogDirection.Sent,
                        DataTransportAuditLogger.AuditLogSource.InstrumentedApp,
                        contentData);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to audit log OTLP request");
            }
        }

        private static async Task LogOtlpResponse(HttpResponseMessage response)
        {
            try
            {
                // Log response content - matches HttpCollectorWire pattern (single received entry with content)
                if (response.Content != null)
                {
                    var responseData = await GetContentForAuditLog(response.Content).ConfigureAwait(false);
                    DataTransportAuditLogger.Log(
                        DataTransportAuditLogger.AuditLogDirection.Received,
                        DataTransportAuditLogger.AuditLogSource.Collector,
                        responseData);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to audit log OTLP response");
            }
        }

        /// <summary>
        /// Retrieves HTTP content for audit logging. For binary protobuf data, encodes as base64
        /// to ensure audit logs contain the actual data sent/received for compliance purposes.
        /// Includes payload size information for auditing purposes.
        /// Matches the pattern used by HttpCollectorWire for consistency.
        /// </summary>
        /// <param name="content">The HTTP content to retrieve</param>
        /// <returns>String representation of the content (base64 for binary, text for other types) with size info</returns>
        private static async Task<string> GetContentForAuditLog(HttpContent content)
        {
            if (content == null)
                return string.Empty;

            try
            {
                var contentType = content.Headers?.ContentType?.MediaType ?? "unknown";
                
                // For binary protobuf content, encode as base64 for audit logging
                if (contentType.StartsWith("application/x-protobuf", StringComparison.OrdinalIgnoreCase) || 
                    contentType.Contains("protobuf"))
                {
                    var bytes = await content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    var base64 = Convert.ToBase64String(bytes);
                    return $"[Protobuf {bytes.Length} bytes, Base64 {base64.Length} chars] {base64}";
                }

                // For text-based content (json, xml, text), return as-is with size
                var textContent = await content.ReadAsStringAsync().ConfigureAwait(false);
                var textBytes = System.Text.Encoding.UTF8.GetByteCount(textContent);
                return $"[Text {textBytes} bytes] {textContent}";
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to read content for audit logging");
                return "[Failed to read content for audit logging]";
            }
        }
    }
}
