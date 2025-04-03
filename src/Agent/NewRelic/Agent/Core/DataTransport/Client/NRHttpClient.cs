// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// IHttpClient implementation that uses System.Net.HttpClient for sending requests.
    /// </summary>
    public class NRHttpClient : HttpClientBase
    {
        private IHttpClientWrapper _httpClientWrapper;
        private readonly TimeSpan _timeout;
        private readonly HttpMethod _httpMethod;

        public NRHttpClient(IWebProxy proxy, IConfiguration configuration) : base(proxy)
        {
            _timeout = TimeSpan.FromMilliseconds((int)configuration.CollectorTimeout);
            _httpMethod = configuration.PutForDataSend ? HttpMethod.Put : HttpMethod.Post;

            var httpHandler = GetHttpHandler(proxy);

            var httpClient = new HttpClient(httpHandler, true) { Timeout = _timeout };
            _httpClientWrapper = new HttpClientWrapper(httpClient, (int)_timeout.TotalMilliseconds);
        }

        [NrExcludeFromCodeCoverage]
        private dynamic GetHttpHandler(IWebProxy proxy)
        {
            // check whether the application is running .NET 6 or later
            if (System.Environment.Version.Major >= 6)
            {
                try
                {
                    var pooledConnectionLifetime = TimeSpan.FromMinutes(5); // an in-use connection will be closed and recycled after 5 minutes
                    var pooledConnectionIdleTimeout = TimeSpan.FromMinutes(1); // a connection that is idle for 1 minute will be closed and recycled

                    Log.Info("Creating a SocketsHttpHandler with PooledConnectionLifetime {ConnectionLifetime}, PooledConnectionIdleTimeout {ConnectionIdleTimeout} and ConnectTimeout {ConnectTimeout}", pooledConnectionLifetime, pooledConnectionIdleTimeout, _timeout);

                    // use reflection to create a SocketsHttpHandler instance and set the timeout values
                    var assembly = Assembly.Load("System.Net.Http");
                    var handlerType = assembly.GetType("System.Net.Http.SocketsHttpHandler");
                    dynamic handler = Activator.CreateInstance(handlerType);

                    handler.PooledConnectionLifetime = pooledConnectionLifetime;
                    handler.PooledConnectionIdleTimeout = pooledConnectionIdleTimeout;
                    handler.ConnectTimeout = _timeout;

                    handler.Proxy = proxy;

                    Log.Info("Current SocketsHttpHandler TLS Configuration (SocketsHttpHandler.SslOptions): {SslOptions}", handler.SslOptions.EnabledSslProtocols);
                    return handler;
                }
                catch (Exception e)
                {
                    Log.Info(e, "Application runtime is .NET 6+ but an exception occurred trying to create SocketsHttpHandler. Falling back to HttpHandler.");
                }
            }

            // if the application is not running .NET 6 or later, use the default HttpClientHandler
            var httpClientHandler = new HttpClientHandler { Proxy = proxy};
            Log.Info("Current HttpClientHandler TLS Configuration (HttpClientHandler.SslProtocols): {SslProtocols}", httpClientHandler.SslProtocols.ToString());

            return httpClientHandler;
        }


        public override IHttpResponse Send(IHttpRequest request)
        {
            try
            {
                using var req = new HttpRequestMessage();
                req.RequestUri = request.Uri;
                req.Method = _httpMethod;
                req.Headers.Add("User-Agent", $"NewRelic-DotNetAgent/{AgentInstallConfiguration.AgentVersion}");
                req.Headers.Add("Connection", "keep-alive");
                req.Headers.Add("Keep-Alive", "true");
                req.Headers.Add("ACCEPT-ENCODING", "gzip");

                foreach (var header in request.Headers)
                {
                    req.Headers.Add(header.Key, header.Value);
                }

                using var content = new ByteArrayContent(request.Content.PayloadBytes);
                var encoding = request.Content.IsCompressed ? request.Content.CompressionType.ToLower() : "identity";
                content.Headers.ContentType = new MediaTypeHeaderValue(request.Content.ContentType);
                content.Headers.Add("Content-Encoding", encoding);
                content.Headers.Add("Content-Length", request.Content.PayloadBytes.Length.ToString());

                req.Content = content;

                foreach (var contentHeader in request.Content.Headers)
                {
                    req.Content.Headers.Add(contentHeader.Key, contentHeader.Value);
                }

                Log.Finest($"Request({request.RequestGuid}: Sending");
                // .ConfigureAwait(false) is used to avoid deadlocks.
                var response = _httpClientWrapper.SendAsync(req).ConfigureAwait(false).GetAwaiter().GetResult();
                Log.Finest($"Request({request.RequestGuid}: Sent");

                var httpResponse = new HttpResponse(request.RequestGuid, response);
                return httpResponse;
            }
            catch (HttpRequestException)
            {
                if (_diagnoseConnectionError)
                {
                    DiagnoseConnectionError(request.Uri.Host);
                }

                throw;
            }
        }

        public override void Dispose()
        {
            _httpClientWrapper?.Dispose();
            base.Dispose();
        }

        // for unit testing
        public void SetHttpClientWrapper(IHttpClientWrapper httpClientWrapper)
        {
            _httpClientWrapper = httpClientWrapper;
        }
    }
}
#endif
