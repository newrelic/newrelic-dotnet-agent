// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
using System.Net.Http;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    public class HttpClientWrapper : IHttpClientWrapper
    {
        private readonly HttpClient _httpClient;

        public HttpClientWrapper(HttpClient client)
        {
            _httpClient = client;
        }


        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public async Task<IHttpResponseMessageWrapper> SendAsync(HttpRequestMessage message)
        {
            return new HttpResponseMessageWrapper(await _httpClient.SendAsync(message));
        }

        public TimeSpan Timeout
        {
            get
            {
                return _httpClient.Timeout;
            }
            set
            {
                _httpClient.Timeout = value;
            }
        }
    }
}
#endif
