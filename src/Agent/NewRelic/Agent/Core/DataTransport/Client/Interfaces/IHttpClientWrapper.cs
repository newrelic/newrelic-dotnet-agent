// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport.Client.Interfaces
{
    public interface IHttpClientWrapper : IDisposable
    {
        Task<IHttpResponseMessageWrapper> SendAsync(HttpRequestMessage message);

        TimeSpan Timeout { get; set; }
    }
}
