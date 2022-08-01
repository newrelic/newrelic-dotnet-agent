// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IHttpClientFactory
    {
        public HttpClient CreateClient(IWebProxy proxy);  
    }

    public class HttpClientFactory : IHttpClientFactory
    {
        private HttpClient _httpClient;

        public HttpClient CreateClient(IWebProxy proxy)
        {
            if (_httpClient == null)
            {
                if (proxy != null)
                {
                    var httpClientHandler = new HttpClientHandler
                    {
                        Proxy = proxy,
                    };

                    _httpClient = new HttpClient(handler: httpClientHandler, disposeHandler: true);
                }
                else
                {
                    _httpClient = new HttpClient();
                }
            }
            return _httpClient;
        }
    }

}
