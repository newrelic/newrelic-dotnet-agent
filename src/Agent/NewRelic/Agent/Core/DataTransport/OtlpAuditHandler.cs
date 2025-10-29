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
    /// Captures requests and responses for compliance auditing while handling binary protobuf content appropriately.
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
                // Audit log failed requests for compliance
                LogOtlpException(request, ex);
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
                    DataTransportAuditLogger.AuditLogSource.OtlpExporter,
                    request.RequestUri?.ToString() ?? "Unknown URI");

                // Log request metadata (safe for binary content)
                var contentInfo = GetSafeContentInfo(request.Content);
                if (!string.IsNullOrEmpty(contentInfo))
                {
                    DataTransportAuditLogger.Log(
                        DataTransportAuditLogger.AuditLogDirection.Sent,
                        DataTransportAuditLogger.AuditLogSource.OtlpExporter,
                        contentInfo);
                }
            }
            catch (Exception ex)
            {
                // Never let audit logging break OTLP export
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
                    DataTransportAuditLogger.AuditLogSource.OtlpExporter,
                    responseInfo);

                // Log response content metadata if present
                if (response.Content != null)
                {
                    var responseContentInfo = GetSafeContentInfo(response.Content);
                    if (!string.IsNullOrEmpty(responseContentInfo))
                    {
                        DataTransportAuditLogger.Log(
                            DataTransportAuditLogger.AuditLogDirection.Received,
                            DataTransportAuditLogger.AuditLogSource.OtlpExporter,
                            responseContentInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never let audit logging break OTLP export
                Log.Debug(ex, "Failed to audit log OTLP response");
            }
        }

        private static void LogOtlpException(HttpRequestMessage request, Exception exception)
        {
            try
            {
                var errorInfo = $"OTLP Export Exception for {request.RequestUri}: {exception.GetType().Name} - {exception.Message}";
                
                DataTransportAuditLogger.Log(
                    DataTransportAuditLogger.AuditLogDirection.Sent,
                    DataTransportAuditLogger.AuditLogSource.OtlpExporter,
                    errorInfo);
            }
            catch (Exception ex)
            {
                // Never let audit logging break OTLP export
                Log.Debug(ex, "Failed to audit log OTLP exception");
            }
        }

        /// <summary>
        /// Generates safe logging information for HTTP content, particularly for binary protobuf data.
        /// Avoids logging full binary content which would be unreadable and potentially large.
        /// </summary>
        /// <param name="content">The HTTP content to generate info for</param>
        /// <returns>Safe string representation of the content metadata</returns>
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

                // For protobuf content, only log metadata, not the actual binary data
                if (contentType.Contains("protobuf", StringComparison.OrdinalIgnoreCase) || 
                    contentType.Contains("application/x-protobuf", StringComparison.OrdinalIgnoreCase))
                {
                    return $"Content: {contentType}, Length: {contentLength} bytes, Encoding: {encoding} [Binary Protobuf Data - Content Not Logged]";
                }

                // For text content types, we could potentially log more detail if needed
                if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase) || 
                    contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
                {
                    return $"Content: {contentType}, Length: {contentLength} bytes, Encoding: {encoding}";
                }

                // Default: just metadata for other content types
                return $"Content: {contentType}, Length: {contentLength} bytes, Encoding: {encoding}";
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to generate safe content info for audit logging");
                return "Content: metadata unavailable";
            }
        }
    }
}