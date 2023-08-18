// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Net;
using System.Threading;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    ///     Pseudo "factory" implementation to get an IHttpClient instance of a NRHttpClient. Registered in DI
    ///     at startup if running in a .NET build
    /// </summary>
    public class NRHttpClientFactory : IHttpClientFactory
    {
        private IHttpClient _httpClient;

        public IHttpClient CreateClient(IWebProxy proxy, IConfiguration configuration)
        {
            if (proxy != null)
            {
                Interlocked.CompareExchange(ref _httpClient, new NRHttpClient(proxy, configuration), null);
            }
            else
            {
                Interlocked.CompareExchange(ref _httpClient, new NRHttpClient(null,configuration), null);
            }

            return _httpClient;
        }
    }
}
#endif
