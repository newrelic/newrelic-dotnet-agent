/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Agent.Core.Logging;

namespace NewRelic.Agent.Core.DataTransport
{
    public class HttpCollectorWire : ICollectorWire
    {
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
        public const int ProtocolVersion = 14;

        private const int CompressMinimumByteLength = 20;
        private bool _diagnoseConnectionError = true;
        private readonly IConfiguration _configuration;

        public HttpCollectorWire(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string SendData(string method, ConnectionInfo connectionInfo, string serializedData)
        {
            try
            {
                var uri = GetUri(method, connectionInfo);

                Log.DebugFormat("Invoking \"{0}\" with : {1}", method, serializedData);
                AuditLog(Direction.Sent, Source.InstrumentedApp, uri);
                AuditLog(Direction.Sent, Source.InstrumentedApp, serializedData);

                var requestPayload = GetRequestPayload(serializedData);
                var request = BuildRequest(uri, connectionInfo, requestPayload);
                var response = SendRequest(request, requestPayload.Data);

                Log.DebugFormat("Received : {0}", response);
                AuditLog(Direction.Received, Source.Collector, response);

                return response;
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
        }

        private static string SendRequest(WebRequest request, byte[] requestPayloadData)
        {
            SendRequestPayload(request, requestPayloadData);
            var response = GetResponse(request);
            return response;
        }

        private static void AuditLog(Direction direction, Source source, string uri)
        {
            var message = string.Format(AuditLogFormat, direction, source, uri);
            Log.Audit(message);
        }
        private string GetUri(string method, ConnectionInfo connectionInfo)
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

            var uriBuilder = new UriBuilder(connectionInfo.HttpProtocol, connectionInfo.Host, (int)connectionInfo.Port, uri.ToString());
            return uriBuilder.Uri.ToString().Replace("%3F", "?");
        }

        private CollectorRequestPayload GetRequestPayload(string serializedData)
        {
            var bytes = new UTF8Encoding().GetBytes(serializedData);

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
                    if (responseBody == null)
                        throw new NullReferenceException("responseBody");

                    return responseBody;
                }
            }
        }

        private static void ThrowExceptionFromHttpWebResponse(string serializedData, HttpWebResponse response)
        {
            try
            {
                throw ExceptionFactories.NewException(response.StatusCode, response.StatusDescription);
            }
            catch (SerializationException)
            {
                Log.ErrorFormat("Invalid json: {0}.  Please report to support@newrelic.com", serializedData);
                throw;
            }
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
