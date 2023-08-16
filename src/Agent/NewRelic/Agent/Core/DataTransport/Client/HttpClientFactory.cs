// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Threading;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    ///     Pseudo "factory" implementation to get an IHttpClient instance of a NRHttpClient. Registered in DI
    ///     at startup if running in a .NET build
    /// </summary>
    public class HttpClientFactory : IHttpClientFactory
    {
        private IHttpClient _httpClient;

        public IHttpClient CreateClient(IWebProxy proxy)
        {
            if (proxy != null)
            {
                Interlocked.CompareExchange(ref _httpClient, new NRHttpClient(proxy), null);
            }
            else
            {
                Interlocked.CompareExchange(ref _httpClient, new NRHttpClient(null), null);
            }

            return _httpClient;
        }
    }
}
