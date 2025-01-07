// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.Utilities
{
    /// <summary>
    /// Thread safe disposable event subscription manager.
    /// </summary>
    public class Subscriptions : IDisposable
    {
        private readonly ConcurrentBag<IDisposable> _subscriptions = new();

        public void Add<T>(Action<T> callback)
        {
            _subscriptions.Add(new EventSubscription<T>(callback));
        }

        public void Add<TRequest, TResponse>(RequestBus<TRequest, TResponse>.RequestHandler requestHandler)
        {
            _subscriptions.Add(new RequestSubscription<TRequest, TResponse>(requestHandler));
        }

        public void AddAsync<T>(Func<Task<T>> taskFunc)
        {
            _subscriptions(new EventSubscriptionAsync<T>(taskFunc));
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
}
