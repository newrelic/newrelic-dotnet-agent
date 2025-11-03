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
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Audit log the outgoing request
            LogOtlpRequest(request);

            HttpResponseMessage response = null;
            try
            {
                // Send the request through the pipeline
                response = await base.SendAsync(request, cancellationToken);

                // Audit log the response
                LogOtlpResponse(request, response);

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

        private static void LogOtlpRequest(HttpRequestMessage request)
        {
            try
            {
                // Log the request URI (with license key obfuscation)
                DataTransportAuditLogger.Log(
                    DataTransportAuditLogger.AuditLogDirection.Sent,
                    DataTransportAuditLogger.AuditLogSource.InstrumentedApp,
                    request.RequestUri?.ToString() ?? "Unknown URI");

                // Log request metadata (for binary content)
                var contentInfo = GetSafeContentInfo(request.Content);
                if (!string.IsNullOrEmpty(contentInfo))
                {
                    DataTransportAuditLogger.Log(
                        DataTransportAuditLogger.AuditLogDirection.Sent,
                        DataTransportAuditLogger.AuditLogSource.InstrumentedApp,
                        contentInfo);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to audit log OTLP request");
            }
        }

        private static void LogOtlpResponse(HttpRequestMessage request, HttpResponseMessage response)
        {
            try
            {
                var responseInfo = $"Response: {response.StatusCode} ({(int)response.StatusCode}) for {request.RequestUri}";
                
                DataTransportAuditLogger.Log(
                    DataTransportAuditLogger.AuditLogDirection.Received,
                    DataTransportAuditLogger.AuditLogSource.Collector,
                    responseInfo);

                // Log response content metadata if present
                if (response.Content != null)
                {
                    var responseContentInfo = GetSafeContentInfo(response.Content);
                    if (!string.IsNullOrEmpty(responseContentInfo))
                    {
                        DataTransportAuditLogger.Log(
                            DataTransportAuditLogger.AuditLogDirection.Received,
                            DataTransportAuditLogger.AuditLogSource.Collector,
                            responseContentInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to audit log OTLP response");
            }
        }

        /// <summary>
        /// Generates logging information for HTTP content, particularly for binary protobuf data.
        /// Avoids logging full binary content which would be unreadable and potentially large.
        /// </summary>
        /// <param name="content">The HTTP content to generate info for</param>
        /// <returns>string representation of the content metadata</returns>
        private static string GetSafeContentInfo(HttpContent content)
        {
            if (content == null)
                return null;

            try
            {
                var contentType = content.Headers?.ContentType?.MediaType ?? "unknown";
                var contentLength = content.Headers?.ContentLength?.ToString() ?? "unknown";
                var encoding = content.Headers?.ContentEncoding != null 
                    ? string.Join(", ", content.Headers.ContentEncoding) 
                    : "none";

                var baseInfo = $"Content: {contentType}, Length: {contentLength} bytes, Encoding: {encoding}";

                // For protobuf content, add warning about binary data not being logged
                if (contentType.StartsWith("application/x-protobuf", StringComparison.OrdinalIgnoreCase) || 
                    contentType.Contains("protobuf", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{baseInfo} [Binary Protobuf Data - Content Not Logged]";
                }

                // All other content types (json, text, xml, etc.) just return basic metadata
                return baseInfo;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to generate safe content info for audit logging");
                return "Content: metadata unavailable";
            }
        }
    }
}