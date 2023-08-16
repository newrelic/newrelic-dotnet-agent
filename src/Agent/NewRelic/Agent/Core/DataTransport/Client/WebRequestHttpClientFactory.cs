// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    ///     Pseudo "factory" implementation to get an instance of a NRWebRequestClient. Registered in DI
    ///     at startup if running in a .NET Framework build. Returns a new instance on every request, as singleton
    ///     management is not required.
    /// </summary>
    public class WebRequestHttpClientFactory : IHttpClientFactory
    {
        private readonly IConfiguration _configuration;

        public WebRequestHttpClientFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IHttpClient CreateClient(IWebProxy proxy)
        {
            return new NRWebRequestClient(proxy, _configuration);
        }
    }
}
