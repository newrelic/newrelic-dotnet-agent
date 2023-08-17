// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport.Client.Interfaces
{
    public interface IHttpClient : IDisposable
    {
        Task<IHttpResponse> SendAsync(IHttpRequest request);
    }
}
