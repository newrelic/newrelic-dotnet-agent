// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http;
using System.Threading;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IHttpClientFactory
    {
        public HttpClient CreateClient(IWebProxy proxy);
        void ResetClient();
    }

    public class HttpClientFactory : IHttpClientFactory
    {
        private HttpClient _httpClient;

        public HttpClient CreateClient(IWebProxy proxy)
        {
            if (proxy != null)
            {
                Interlocked.CompareExchange(ref _httpClient, new HttpClient(new HttpClientHandler
                {
                    Proxy = proxy,
                }, disposeHandler: true), null);
            }
            else
            {
                Interlocked.CompareExchange(ref _httpClient, new HttpClient(), null);
            }

            return _httpClient;
        }

        public void ResetClient()
        {
            var oldClient = Interlocked.Exchange(ref _httpClient, null);
            oldClient?.Dispose();
        }
    }

}
