/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using NewRelic.WeakActions;

namespace NewRelic.Dispatchers
{
    public class WeakEventSubscription<T> : IDisposable
    {
        private readonly IWeakAction<T> _callback;

        public WeakEventSubscription(Action<T> callback)
        {
            _callback = WeakActionUtilities.MakeWeak(callback, garbageCollectedAction => EventBus<T>.Unsubscribe(garbageCollectedAction));
            EventBus<T>.Subscribe(_callback);
        }

        public void Dispose()
        {
            EventBus<T>.Unsubscribe(_callback);
        }
    }
}
