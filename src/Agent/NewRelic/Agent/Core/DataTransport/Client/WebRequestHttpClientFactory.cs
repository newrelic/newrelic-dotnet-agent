// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
#if NETFRAMEWORK
using System.Net;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    ///     Pseudo "factory" implementation to get an instance of a NRWebRequestClient. Registered in DI
    ///     at startup if running in a .NET Framework build. Returns a new instance on every request, as singleton
    ///     management is not required.
    /// </summary>
    public class WebRequestHttpClientFactory : IHttpClientFactory
    {
        public WebRequestHttpClientFactory()
        {
        }

        public IHttpClient CreateClient(IWebProxy proxy, IConfiguration configuration)
        {
            return new NRWebRequestClient(proxy, configuration);
        }
    }
}
#endif
