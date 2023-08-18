// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;
using System.Net;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// IHttpClient implementation that uses WebRequest to send requests
    /// </summary>
    public class NRWebRequestClient : HttpClientBase
    {
        private readonly IConfiguration _configuration;
        private HttpWebRequest _httpWebRequest;

        private Func<Uri, HttpWebRequest> _getHttpWebRequestFunc = uri => (HttpWebRequest)WebRequest.Create(uri);

        public NRWebRequestClient(IWebProxy proxy, IConfiguration configuration) : base(proxy)
        {
            _configuration = configuration;
        }

        public override async Task<IHttpResponse> SendAsync(IHttpRequest request)
        {
            try
            {
                _httpWebRequest = _getHttpWebRequestFunc(request.Uri);

                // If a null assignment is made it will bypass the default (IE) proxy settings 
                // bypassing those settings could cause 504s to be thrown where the user has 
                // implemented a proxy via IE instead of implementing an external proxy and declaring the values in the New Relic config
                if (_proxy != null)
                {
                    _httpWebRequest.Proxy = _proxy;
                }

                _httpWebRequest.KeepAlive = true;
                _httpWebRequest.ContentType = request.Content.ContentType;
                _httpWebRequest.UserAgent = $"NewRelic-DotNetAgent/{AgentInstallConfiguration.AgentVersion}";
                _httpWebRequest.Method = _configuration.PutForDataSend ? "PUT" : "POST";
                _httpWebRequest.ContentLength = request.Content.PayloadBytes.Length;

                _httpWebRequest.Headers.Add("ACCEPT-ENCODING", "gzip");
                _httpWebRequest.Headers.Add("CONTENT-ENCODING", request.Content.Encoding);

                foreach (var header in request.Headers)
                {
                    _httpWebRequest.Headers.Add(header.Key, header.Value);
                }

                using (var outputStream = _httpWebRequest.GetRequestStream())
                {
                    if (outputStream == null)
                    {
                        throw new NullReferenceException("outputStream");
                    }

                    // .ConfigureAwait(false) is required here for some reason
                    await outputStream.WriteAsync(request.Content.PayloadBytes, 0, (int)_httpWebRequest.ContentLength).ConfigureAwait(false);
                }

                // .ConfigureAwait(false) is required here for some reason
                var resp = (HttpWebResponse)await _httpWebRequest.GetResponseAsync().ConfigureAwait(false);

                return new WebRequestClientResponse(request.RequestGuid, resp);
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse httpWebResponse)
                    return new WebRequestClientResponse(request.RequestGuid, httpWebResponse);

                if (_diagnoseConnectionError)
                {
                    DiagnoseConnectionError(request.Uri.Host);
                }

                throw;
            }
        }

        public override void Dispose()
        {
        }

        // for unit testing only
        public void SetHttpWebRequestFunc(Func<Uri, HttpWebRequest> getHttpWebRequestFunc )
        {
            _getHttpWebRequestFunc = getHttpWebRequestFunc;
        }
    }
}
#endif
