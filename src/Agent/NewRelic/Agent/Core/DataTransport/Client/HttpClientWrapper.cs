// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NewRelic.Agent.Extensions.Logging;

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
            using var cts = new CancellationTokenSource(_timeoutMilliseconds);
            try
            {
                var httpResponseMessage = await _httpClient.SendAsync(message, cts.Token).ConfigureAwait(false);
                return new HttpResponseMessageWrapper(httpResponseMessage);
            }
            catch (Exception e)
            {
                Log.Debug(cts.IsCancellationRequested
                    ? $"HttpClient.SendAsync() timed out after {_timeoutMilliseconds}ms."
                    : $"HttpClient.SendAsync() threw an unexpected exception: {e}");

                throw;
            }
        }
    }
}
#endif
