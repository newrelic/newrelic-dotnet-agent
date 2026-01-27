// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Utilities;

public class RequestSubscription<TRequest, TResponse> : IDisposable
{
    private readonly RequestBus<TRequest, TResponse>.RequestHandler _requestHandler;

    public RequestSubscription(RequestBus<TRequest, TResponse>.RequestHandler requestHandler)
    {
        _requestHandler = requestHandler;
        RequestBus<TRequest, TResponse>.AddResponder(_requestHandler);
    }

    public void Dispose()
    {
        RequestBus<TRequest, TResponse>.RemoveResponder(_requestHandler);
    }
}