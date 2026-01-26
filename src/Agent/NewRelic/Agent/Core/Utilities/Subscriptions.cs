// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Utilities;

/// <summary>
/// Thread safe disposable event subscription manager.
/// </summary>
public class Subscriptions : IDisposable
{
    private readonly ICollection<IDisposable> _subscriptions = new List<IDisposable>();

    public void Add<T>(Action<T> callback)
    {
        lock (_subscriptions)
        {
            _subscriptions.Add(new EventSubscription<T>(callback));
        }
    }

    public void Add<TRequest, TResponse>(RequestBus<TRequest, TResponse>.RequestHandler requestHandler)
    {
        lock (_subscriptions)
        {
            _subscriptions.Add(new RequestSubscription<TRequest, TResponse>(requestHandler));
        }
    }

    public void Dispose()
    {
        lock (_subscriptions)
        {
            foreach (var subscription in _subscriptions)
                if (subscription != null)
                    subscription.Dispose();

            _subscriptions.Clear();
        }
    }
}