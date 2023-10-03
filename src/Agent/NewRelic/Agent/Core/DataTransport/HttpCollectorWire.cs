// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using NewRelic.Agent.Core.DataTransport.Client;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport
{
    public class HttpCollectorWire : ICollectorWire
    {

        private readonly IHttpClientFactory _httpClientFactory;

        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, string> _requestHeadersMap;
        private static Dictionary<string, string> _emptyRequestHeadersMap = new Dictionary<string, string>();
        private readonly IAgentHealthReporter _agentHealthReporter;

        public HttpCollectorWire(IConfiguration configuration, IAgentHealthReporter agentHealthReporter, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _requestHeadersMap = _emptyRequestHeadersMap;
            _agentHealthReporter = agentHealthReporter;
            _httpClientFactory = httpClientFactory;
        }

        public HttpCollectorWire(IConfiguration configuration, Dictionary<string, string> requestHeadersMap, IAgentHealthReporter agentHealthReporter, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _requestHeadersMap = requestHeadersMap ?? _emptyRequestHeadersMap;
            _agentHealthReporter = agentHealthReporter;
            _httpClientFactory = httpClientFactory;
        }

        public string SendData(string method, ConnectionInfo connectionInfo, string serializedData, Guid requestGuid)
        {
            HttpRequest request = null;
            try
            {

                var httpClient = _httpClientFactory.CreateClient(connectionInfo.Proxy, _configuration);

                request = new HttpRequest(_configuration)
                {
                    Endpoint = method,
                    ConnectionInfo = connectionInfo,
                    RequestGuid = requestGuid,
                    Content =
                        {
                            SerializedData = serializedData,
                            ContentType = "application/octet-stream",
                        }
                };

                foreach (var header in _requestHeadersMap)
                    request.Headers.Add(header.Key, header.Value);

                using var response = httpClient.SendAsync(request).GetAwaiter().GetResult();

                var responseContent = response.GetContentAsync().GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    ThrowExceptionFromHttpResponseMessage(serializedData, response.StatusCode, responseContent, requestGuid);
                }

                DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Sent, DataTransportAuditLogger.AuditLogSource.InstrumentedApp, request.Uri.ToString());
                DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Sent, DataTransportAuditLogger.AuditLogSource.InstrumentedApp, request.Content.SerializedData);

                _agentHealthReporter.ReportSupportabilityDataUsage("Collector", method, request.Content.UncompressedByteCount, new UTF8Encoding().GetBytes(responseContent).Length);

                // Possibly combine these logs? makes parsing harder in tests...
                Log.Debug("Request({0}): Invoked \"{1}\" with : {2}", requestGuid, method, serializedData);
                Log.Debug("Request({0}): Invocation of \"{1}\" yielded response : {2}", requestGuid, method, responseContent);

                DataTransportAuditLogger.Log(DataTransportAuditLogger.AuditLogDirection.Received, DataTransportAuditLogger.AuditLogSource.Collector, responseContent);

                return responseContent;
            }
            catch (PayloadSizeExceededException ex)
            {
                // Log that the payload is being dropped
                Log.Error(ex, "Request({0}): Dropped large payload: size: {1}, max_payload_size_bytes={2}",
                    request!.RequestGuid, request!.Content.PayloadBytes.Length, _configuration.CollectorMaxPayloadSizeInBytes);

                _agentHealthReporter.ReportSupportabilityPayloadsDroppeDueToMaxPayloadSizeLimit(method);

                return Constants.EmptyResponseBody; // mimics what is sent by NoOpCollectorWire
            }
        }

        private static void ThrowExceptionFromHttpResponseMessage(string serializedData, HttpStatusCode statusCode, string responseText, Guid requestGuid)
        {
            if (statusCode == HttpStatusCode.UnsupportedMediaType)
            {
                Log.Error("Request({0}): Had invalid json: {1}. Please report to support@newrelic.com", requestGuid, serializedData);
            }

            // P17: Not supposed to read/use the exception message in the connect response body. We are still going to log it, carefully, since it is very useful for support.
            Log.Error("Request({0}): Received HTTP status code {1} with message {2}. Request content was: {3}", requestGuid, statusCode.ToString(), responseText, serializedData);

            throw new HttpException(statusCode, responseText);
        }
    }
}
