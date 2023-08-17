// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// IHttpClient implementation that uses System.Net.HttpClient for sending requests.
    /// </summary>
    public class NRHttpClient : HttpClientBase
    {
        private HttpClient _httpClient;

        public NRHttpClient(IWebProxy proxy) : base(proxy)
        {
            _httpClient = new HttpClient(new HttpClientHandler { Proxy = proxy }, true);
        }

        public override async Task<IHttpResponse> SendAsync(IHttpRequest request)
        {
            try
            {
                var req = new HttpRequestMessage { RequestUri = request.Uri, Method = request.Method.ToHttpMethod() };
                req.Headers.Add("User-Agent", $"NewRelic-DotNetAgent/{AgentInstallConfiguration.AgentVersion}");

                req.Headers.Add("Timeout", ((int)request.Timeout.TotalSeconds).ToString());
                _httpClient.Timeout = request.Timeout;

                req.Headers.Add("Connection", "keep-alive");
                req.Headers.Add("Keep-Alive", "true");
                req.Headers.Add("ACCEPT-ENCODING", "gzip");

                foreach (var header in request.Headers)
                {
                    req.Headers.Add(header.Key, header.Value);
                }

                var content = new ByteArrayContent(request.Content.PayloadBytes);
                var encoding = request.Content.IsCompressed ? request.Content.CompressionType.ToLower() : "identity";
                content.Headers.ContentType = new MediaTypeHeaderValue(request.Content.ContentType);
                content.Headers.Add("Content-Encoding", encoding);
                content.Headers.Add("Content-Length", request.Content.PayloadBytes.Length.ToString());

                req.Content = content;

                foreach (var contentHeader in request.Content.Headers)
                {
                    req.Content.Headers.Add(contentHeader.Key, contentHeader.Value);
                }

                var response = await _httpClient.SendAsync(req);

                var httpResponse = new HttpResponse(request.RequestGuid, new HttpResponseMessageWrapper(response));
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
            _httpClient?.Dispose();
        }

        // for unit testing
        public void SetHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
    }
}
#endif
