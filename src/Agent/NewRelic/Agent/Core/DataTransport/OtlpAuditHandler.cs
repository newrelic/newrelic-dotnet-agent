// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport.Client;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport;

/// <summary>
/// DelegatingHandler that provides audit logging for OTLP (OpenTelemetry Protocol) HTTP communications.
/// Captures requests and responses for compliance auditing while handling binary protobuf content.
/// </summary>
public class OtlpAuditHandler : DelegatingHandler
{
    private readonly IAgentHealthReporter _agentHealthReporter;

    public OtlpAuditHandler(IAgentHealthReporter agentHealthReporter = null)
    {
        _agentHealthReporter = agentHealthReporter;
    }

    /// <summary>
    /// Sends an HTTP request and logs request/response data for audit purposes.
    /// </summary>
    /// <param name="request">The HTTP request message to send</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>The HTTP response message</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Log.Finest("Exporting OTLP metrics to {RequestUri}", request.RequestUri);
            
        // Audit log the outgoing request and capture size - single read for both audit log and metrics
        var bytesSent = await LogOtlpRequest(request).ConfigureAwait(false);

        HttpResponseMessage response = null;
        try
        {
            // Send the request through the pipeline
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // Audit log the response and capture size - single read for both audit log and metrics
            var bytesReceived = await LogOtlpResponse(response).ConfigureAwait(false);

            // Report supportability metrics for data usage
            _agentHealthReporter?.ReportSupportabilityDataUsage("OTLP", "Metrics", bytesSent, bytesReceived);

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

    private static async Task<long> LogOtlpRequest(HttpRequestMessage request)
    {
        long bytesSent = 0;
        try
        {
            // Log the request URI (with license key obfuscation)
            DataTransportAuditLogger.Log(
                DataTransportAuditLogger.AuditLogDirection.Sent,
                DataTransportAuditLogger.AuditLogSource.InstrumentedApp,
                request.RequestUri?.ToString() ?? "Unknown URI");

            // Log request content and capture size - single read for both purposes
            if (request.Content != null)
            {
                var (contentData, bytesRead) = await GetContentForAuditLogWithSize(request.Content).ConfigureAwait(false);
                bytesSent = bytesRead;
                    
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
        return bytesSent;
    }

    private static async Task<long> LogOtlpResponse(HttpResponseMessage response)
    {
        long bytesReceived = 0;
        try
        {
            // Log response content and capture size - single read for both purposes
            if (response.Content != null)
            {
                var (responseData, bytesRead) = await GetContentForAuditLogWithSize(response.Content).ConfigureAwait(false);
                bytesReceived = bytesRead;
                    
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
        return bytesReceived;
    }

    /// <summary>
    /// Retrieves HTTP content for audit logging with byte size. For binary protobuf data, encodes as base64
    /// to ensure audit logs contain the actual data sent/received for compliance purposes.
    /// Includes payload size information for auditing purposes.
    /// Matches the pattern used by HttpCollectorWire for consistency.
    /// </summary>
    /// <param name="content">The HTTP content to retrieve</param>
    /// <returns>Tuple of (formatted content string, byte count)</returns>
    private static async Task<(string, long)> GetContentForAuditLogWithSize(HttpContent content)
    {
        if (content == null)
            return (string.Empty, 0);

        try
        {
            var contentType = content.Headers?.ContentType?.MediaType ?? "unknown";
                
            // For binary protobuf content, encode as base64 for audit logging
            if (contentType.StartsWith("application/x-protobuf", StringComparison.OrdinalIgnoreCase) || 
                contentType.Contains("protobuf"))
            {
                var bytes = await content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var base64 = Convert.ToBase64String(bytes);
                return ($"[Protobuf {bytes.Length} bytes, Base64 {base64.Length} chars] {base64}", bytes.Length);
            }

            // For text-based content (json, xml, text), return as-is with size
            var textContent = await content.ReadAsStringAsync().ConfigureAwait(false);
            var textBytes = System.Text.Encoding.UTF8.GetByteCount(textContent);
            return ($"[Text {textBytes} bytes] {textContent}", textBytes);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to read content for audit logging");
            return ("[Failed to read content for audit logging]", 0);
        }
    }
}