// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    public interface IHttpResponseMessageWrapper : IDisposable
    {
        IHttpContentWrapper Content { get; }
        bool IsSuccessStatusCode { get; }
        HttpStatusCode StatusCode { get; }
    }
}
