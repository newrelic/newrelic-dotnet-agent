// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    public interface IHttpClientFactory
    {
        public IHttpClient CreateClient(IWebProxy proxy);
    }
}
