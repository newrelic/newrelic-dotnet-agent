// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Core;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport
{
    public class HttpCollectorWire : ICollectorWire
    {

        private readonly IHttpClientFactory _httpClientFactory;


        /// <summary>
        /// This represents the origin or source of data. Used for audit logs.
        /// </summary>
        private enum Source
        {
            Collector = 1,
            Beacon = 2,
            InstrumentedApp = 3
        }

        /// <summary>
        /// This represents the direction or flow of data. Used for audit logs.
        /// </summary>
        private enum Direction
        {
            Sent = 1,
            Received = 2
        }

        public const string AuditLogFormat = "Data {0} from the {1} : {2}";
        public const int ProtocolVersion = 17;
        private const int CompressMinimumByteLength = 20;
        private const string EmptyResponseBody = "{}";
        private const string LicenseKeyParameterName = "license_key";

        private bool _diagnoseConnectionError = true;
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

        public async Task<string> SendDataAsync(string method, ConnectionInfo connectionInfo, string serializedData, Guid requestGuid)
        {
            try
            {
                var uri = GetUri(method, connectionInfo);

                AuditLog(Direction.Sent, Source.InstrumentedApp, uri.ToString());
                AuditLog(Direction.Sent, Source.InstrumentedApp, serializedData);

                var bytes = new UTF8Encoding().GetBytes(serializedData);
                var uncompressedByteCount = bytes.Length;
                var requestPayload = GetRequestPayload(bytes);

                if (requestPayload.Data.Length > _configuration.CollectorMaxPayloadSizeInBytes)
                {
                    // Log that the payload is being dropped
                    Log.ErrorFormat("Request({0}): Dropped large payload: size: {1}, max_payload_size_bytes={2}", requestGuid, requestPayload.Data.Length, _configuration.CollectorMaxPayloadSizeInBytes);
                    _agentHealthReporter.ReportSupportabilityPayloadsDroppeDueToMaxPayloadSizeLimit(method);
                    return "{}"; // mimics what is sent by NoOpCollectorWire
                }

                var httpClient = _httpClientFactory.CreateClient(connectionInfo.Proxy);

                using (var request = new HttpRequestMessage { RequestUri = uri })
                {
                    request.Headers.Add("User-Agent", $"NewRelic-DotNetAgent/{AgentInstallConfiguration.AgentVersion}");
                    request.Headers.Add("Timeout", _configuration.CollectorTimeout.ToString());

                    request.Headers.Add("Connection", "keep-alive");
                    request.Headers.Add("Keep-Alive", "true");
                    request.Headers.Add("ACCEPT-ENCODING", "gzip");

                    var content = new ByteArrayContent(requestPayload.Data);
                    var encoding = (requestPayload.IsCompressed) ? requestPayload.CompressionType.ToLower() : "identity";
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    content.Headers.Add("Content-Encoding", encoding);
                    content.Headers.Add("Content-Length", requestPayload.Data.Length.ToString());

                    request.Content = content;

                    foreach (var header in _requestHeadersMap)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }

                    if (_configuration.PutForDataSend)
                    {
                        request.Method = HttpMethod.Put;
                    }
                    else
                    {
                        request.Method = HttpMethod.Post;
                    }

                    try
                    {
                        Log.DebugFormat("Request({0}): Invoking httpClient.SendAsync()", requestGuid);
                        using (var response = await httpClient.SendAsync(request))
                        {
                            var responseContent = GetResponseContent(response, requestGuid);

                            _agentHealthReporter.ReportSupportabilityDataUsage("Collector", method, uncompressedByteCount, new UTF8Encoding().GetBytes(responseContent).Length);

                            // Possibly combine these logs? makes parsing harder in tests...
                            Log.DebugFormat("Request({0}): Invoked \"{1}\" with : {2}", requestGuid, method, serializedData);
                            Log.DebugFormat("Request({0}): Invocation of \"{1}\" yielded response : {2}", requestGuid, method, responseContent);

                            AuditLog(Direction.Received, Source.Collector, responseContent);
                            if (!response.IsSuccessStatusCode)
                            {
                                ThrowExceptionFromHttpResponseMessage(serializedData, response.StatusCode, responseContent, requestGuid);
                            }

                            return responseContent;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Debug(e);
                        throw;
                    }
                }
            }
            catch (HttpRequestException)
            {
                if (_diagnoseConnectionError)
                {
                    DiagnoseConnectionError(connectionInfo);
                }

                _httpClientFactory.ResetClient();
                throw;
            }
        }

        private static void AuditLog(Direction direction, Source source, string uri)
        {
            var message = string.Format(AuditLogFormat, direction, source, Strings.ObfuscateLicenseKeyInAuditLog(uri, LicenseKeyParameterName));
            Logging.AuditLog.Log(message);
        }

        private Uri GetUri(string method, ConnectionInfo connectionInfo)
        {
            var uri = new StringBuilder("/agent_listener/invoke_raw_method?method=")
                .Append(method)
                .Append($"&{LicenseKeyParameterName}=")
                .Append(_configuration.AgentLicenseKey)
                .Append("&marshal_format=json")
                .Append("&protocol_version=")
                .Append(ProtocolVersion);

            if (_configuration.AgentRunId != null)
                uri.Append("&run_id=").Append(_configuration.AgentRunId);

            var uriBuilder = new UriBuilder(connectionInfo.HttpProtocol, connectionInfo.Host, connectionInfo.Port, uri.ToString());
            return new Uri(uriBuilder.Uri.ToString().Replace("%3F", "?"));
        }

        private CollectorRequestPayload GetRequestPayload(byte[] bytes)
        {
            var shouldCompress = bytes.Length >= CompressMinimumByteLength;

            string compressionType = null;
            if (shouldCompress)
            {
                compressionType = _configuration.CompressedContentEncoding;
                bytes = DataCompressor.Compress(bytes, compressionType);
            }

            var payload = new CollectorRequestPayload(shouldCompress, compressionType, bytes);

            return payload;
        }

        private string GetResponseContent(HttpResponseMessage response, Guid requestGuid)
        {
            try
            {
                var responseStream = response.Content?.ReadAsStreamAsync().GetAwaiter().GetResult();

                if (responseStream == null)
                    return EmptyResponseBody;

                var contentTypeEncoding = response.Content.Headers.ContentEncoding;
                if (contentTypeEncoding.Contains("gzip"))
                {
                    responseStream = new GZipStream(responseStream, CompressionMode.Decompress);
                }

                using (responseStream)
                using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    var responseBody = reader.ReadLine();

                    if (responseBody != null)
                    {
                        return responseBody;
                    }
                    else
                    {
                        return EmptyResponseBody;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Request({0}): Unable to parse response body with exception: {1}", requestGuid, ex.Message);
                return EmptyResponseBody;
            }
        }

        private static void ThrowExceptionFromHttpResponseMessage(string serializedData, HttpStatusCode statusCode, string responseText, Guid requestGuid)
        {
            if (statusCode == HttpStatusCode.UnsupportedMediaType)
            {
                Log.ErrorFormat("Request({0}): Had invalid json: {1}. Please report to support@newrelic.com", requestGuid, serializedData);
            }

            // P17: Not supposed to read/use the exception message in the connect response body. We are still going to log it, carefully, since it is very useful for support.
            Log.ErrorFormat("Request({0}): Received HTTP status code {1} with message {2}. Request content was: {3}", requestGuid, statusCode.ToString(), responseText, serializedData);

            throw new HttpException(statusCode, responseText);
        }

        private void DiagnoseConnectionError(ConnectionInfo connectionInfo)
        {
            _diagnoseConnectionError = false;
            try
            {
                IPAddress address;
                if (!IPAddress.TryParse(connectionInfo.Host, out address))
                {
                    Dns.GetHostEntry(connectionInfo.Host);
                }
            }
            catch (Exception)
            {
                Log.ErrorFormat("Unable to resolve host name \"{0}\"", connectionInfo.Host);
            }

            TestConnection(connectionInfo);
        }

        private static void TestConnection(ConnectionInfo connectionInfo)
        {
            const string testAddress = "http://www.google.com";
            try
            {
                using (var wc = new WebClient())
                {
                    if (connectionInfo.Proxy != null)
                        wc.Proxy = connectionInfo.Proxy;

                    wc.DownloadString(testAddress);
                }
                Log.InfoFormat("Connection test to \"{0}\" succeeded", testAddress);
            }
            catch (Exception)
            {
                var message = $"Connection test to \"{testAddress}\" failed.";
                if (connectionInfo.Proxy != null)
                    message += $" Check your proxy settings ({connectionInfo.Proxy.Address})";
                Log.Error(message);
            }
        }

    }
}
