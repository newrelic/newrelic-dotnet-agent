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

        private bool? _hasProxy;

        public IHttpClient CreateClient(IWebProxy proxy, IConfiguration configuration)
        {
            var proxyRequired = (proxy != null);
            if (_httpClient != null && (_hasProxy == proxyRequired))
            {
                return _httpClient;
            }

            _hasProxy = proxyRequired;
            return _httpClient = new NRHttpClient(proxy, configuration);
        }
    }
}
#endif
