// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport.Client.Interfaces
{
    public interface IHttpClientFactory
    {
        public IHttpClient CreateClient(IWebProxy proxy, IConfiguration configuration);
    }
}
