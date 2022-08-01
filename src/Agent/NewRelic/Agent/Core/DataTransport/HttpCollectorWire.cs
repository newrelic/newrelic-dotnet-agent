// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Exceptions;
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

        public string SendData(string method, ConnectionInfo connectionInfo, string serializedData)
        {
            try
            {
                var uri = GetUri(method, connectionInfo);
                Log.DebugFormat("Invoking \"{0}\" with : {1}", method, serializedData);
                AuditLog(Direction.Sent, Source.InstrumentedApp, uri.ToString());
                AuditLog(Direction.Sent, Source.InstrumentedApp, serializedData);

                var bytes = new UTF8Encoding().GetBytes(serializedData);
                var uncompressedByteCount = bytes.Length;
                var requestPayload = GetRequestPayload(bytes);

                var httpClient = _httpClientFactory.CreateClient(connectionInfo.Proxy);

                httpClient.DefaultRequestHeaders.Add("User-Agent", $"NewRelic-DotNetAgent/{AgentInstallConfiguration.AgentVersion}");
                httpClient.DefaultRequestHeaders.Add("Timeout", _configuration.CollectorTimeout.ToString());

                httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                httpClient.DefaultRequestHeaders.Add("Keep-Alive", "true");
                httpClient.DefaultRequestHeaders.Add("ACCEPT-ENCODING", "gzip");

                var request = new HttpRequestMessage
                {
                    RequestUri = uri
                };

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

                var response = httpClient.SendAsync(request).GetAwaiter().GetResult();

                var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();

                if (responseStream == null)
                    throw new NullReferenceException("responseStream");
                if (response.Headers == null)
                    throw new NullReferenceException("response.Headers");

                var contentTypeEncoding = response.Content.Headers.ContentEncoding;
                if (contentTypeEncoding.Contains("gzip"))
                    responseStream = new GZipStream(responseStream, CompressionMode.Decompress);

                using (responseStream)
                using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    var responseBody = reader.ReadLine();

                    if (responseBody != null)
                        return responseBody;
                    else
                        return EmptyResponseBody;
                }

            }
            catch (WebException ex)
            {
                var httpWebResponse = ex.Response as HttpWebResponse;
                if (httpWebResponse != null)
                    ThrowExceptionFromHttpWebResponse(serializedData, httpWebResponse);

                if (_diagnoseConnectionError)
                    DiagnoseConnectionError(connectionInfo);

                throw;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //public string SendData(string method, ConnectionInfo connectionInfo, string serializedData)
        //{
        //    try
        //    {
        //        var uri = GetUri(method, connectionInfo).ToString().Replace("%3F", "?");

        //        Log.DebugFormat("Invoking \"{0}\" with : {1}", method, serializedData);
        //        AuditLog(Direction.Sent, Source.InstrumentedApp, uri);
        //        AuditLog(Direction.Sent, Source.InstrumentedApp, serializedData);

        //        var bytes = new UTF8Encoding().GetBytes(serializedData);
        //        var uncompressedByteCount = bytes.Length;
        //        var requestPayload = GetRequestPayload(bytes);
        //        var request = BuildRequest(uri, connectionInfo, requestPayload);

        //        // Check serializedData length < MaxPayloadSizeInBytes before sending
        //        // if > silently drop the payload for now, ideally want to be able split the data
        //        // into chunks to send
        //        if (requestPayload.Data.Length > _configuration.CollectorMaxPayloadSizeInBytes)
        //        {
        //            // Log that the payload is being dropped. 
        //            Log.ErrorFormat("Dropped large payload: size: {0}, max_payload_size_bytes={1}", requestPayload.Data.Length, _configuration.CollectorMaxPayloadSizeInBytes);
        //            _agentHealthReporter.ReportSupportabilityPayloadsDroppeDueToMaxPayloadSizeLimit(method);
        //            return "{}"; // mimics what is sent by NoOpCollectorWire
        //        }

        //        var response = SendRequest(request, requestPayload.Data);

        //        _agentHealthReporter.ReportSupportabilityDataUsage("Collector", method, uncompressedByteCount, new UTF8Encoding().GetBytes(response).Length);

        //        Log.DebugFormat("Received : {0}", response);
        //        AuditLog(Direction.Received, Source.Collector, response);

        //        return response;
        //    }
        //    catch (WebException ex)
        //    {
        //        var httpWebResponse = ex.Response as HttpWebResponse;
        //        if (httpWebResponse != null)
        //            ThrowExceptionFromHttpWebResponse(serializedData, httpWebResponse);

        //        if (_diagnoseConnectionError)
        //            DiagnoseConnectionError(connectionInfo);

        //        throw;
        //    }
        //}

        private static string SendRequest(WebRequest request, byte[] requestPayloadData)
        {
            SendRequestPayload(request, requestPayloadData);
            var response = GetResponse(request);
            return response;
        }

        private static void AuditLog(Direction direction, Source source, string uri)
        {
            var message = string.Format(AuditLogFormat, direction, source, uri);
            Logging.AuditLog.Log(message);
        }

        private Uri GetUri(string method, ConnectionInfo connectionInfo)
        {
            var uri = new StringBuilder("/agent_listener/invoke_raw_method?method=")
                .Append(method)
                .Append("&license_key=")
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

        private WebRequest BuildRequest(string uri, ConnectionInfo connectionInfo, CollectorRequestPayload requestCollectorRequestPayload)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            if (request.Headers == null)
                throw new NullReferenceException("request.headers");

            request.KeepAlive = true;

            // If a null assignment is made it will bypass the default (IE) proxy settings 
            // bypassing those settings could cause 504s to be thrown where the user has 
            // implemented a proxy via IE instead of implementing an external proxy and declaring the values in the New Relic config
            if (connectionInfo.Proxy != null)
            {
                request.Proxy = connectionInfo.Proxy;
            }

            request.Timeout = (int)_configuration.CollectorTimeout;
            request.ContentType = "application/octet-stream";
            request.UserAgent = $"NewRelic-DotNetAgent/{AgentInstallConfiguration.AgentVersion}";

            request.Method = _configuration.PutForDataSend ? "PUT" : "POST";
            request.ContentLength = requestCollectorRequestPayload.Data.Length;

            request.Headers.Add("ACCEPT-ENCODING", "gzip");

            var encoding = (requestCollectorRequestPayload.IsCompressed) ? requestCollectorRequestPayload.CompressionType.ToLower() : "identity";
            request.Headers.Add("CONTENT-ENCODING", encoding);

            foreach (var header in _requestHeadersMap)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            return request;
        }

        private static void SendRequestPayload(WebRequest request, byte[] payload)
        {
            using (var outputStream = request.GetRequestStream())
            {
                if (outputStream == null)
                    throw new NullReferenceException("outputStream");

                outputStream.Write(payload, 0, (int)request.ContentLength);
            }
        }

        private static string GetResponse(WebRequest request)
        {
            using (var response = request.GetResponse())
            {
                var responseStream = response.GetResponseStream();
                if (responseStream == null)
                    throw new NullReferenceException("responseStream");
                if (response.Headers == null)
                    throw new NullReferenceException("response.Headers");

                var contentTypeEncoding = response.Headers.Get("content-encoding");
                if ("gzip".Equals(contentTypeEncoding))
                    responseStream = new GZipStream(responseStream, CompressionMode.Decompress);

                using (responseStream)
                using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    var responseBody = reader.ReadLine();
                    return responseBody != null ? responseBody : EmptyResponseBody;
                }
            }
        }

        private static void ThrowExceptionFromHttpWebResponse(string serializedData, HttpWebResponse response)
        {
            if (response.StatusCode == HttpStatusCode.UnsupportedMediaType)
            {
                Log.ErrorFormat("Invalid json: {0}.  Please report to support@newrelic.com", serializedData);
            }

            // P17: Not supposed to read/use the exception message in the connect response body. We are still going to log it, carefully, since it is very useful for support.
            try
            {
                using (var reader = new StreamReader(response.GetResponseStream(), ASCIIEncoding.ASCII))
                {
                    var responseText = reader.ReadToEnd();
                    Log.ErrorFormat("Received HTTP status code {0} with message {1}", response.StatusCode.ToString(), responseText);
                }
            }
            catch (Exception exception)
            {
                Log.ErrorFormat("Unable to parse repsonse body with {0}", exception.Message);
            }

            throw new HttpException(response.StatusCode, response.StatusDescription);
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
