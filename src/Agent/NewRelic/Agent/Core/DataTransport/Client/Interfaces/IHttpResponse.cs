// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;

namespace NewRelic.Agent.Core.DataTransport.Client.Interfaces;

public interface IHttpResponse : IDisposable
{
    bool IsSuccessStatusCode { get; }
    HttpStatusCode StatusCode { get; }
    string GetContent();
}
