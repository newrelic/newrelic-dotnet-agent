// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.DataTransport.Client.Interfaces;

public interface IHttpClient : IDisposable
{
    IHttpResponse Send(IHttpRequest request);
}
