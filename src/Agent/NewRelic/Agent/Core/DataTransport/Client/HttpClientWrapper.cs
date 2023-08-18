// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    public class HttpClientWrapper : IHttpClientWrapper
    {
        private readonly HttpClient _httpClient;
        private readonly int _timeoutMilliseconds;

        public HttpClientWrapper(HttpClient client, int timeoutMilliseconds)
        {
            _httpClient = client;
            _timeoutMilliseconds = timeoutMilliseconds;
        }


        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public async Task<IHttpResponseMessageWrapper> SendAsync(HttpRequestMessage message)
        {
            var cts = new CancellationTokenSource(_timeoutMilliseconds);
            return new HttpResponseMessageWrapper(await _httpClient.SendAsync(message, cts.Token));
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
