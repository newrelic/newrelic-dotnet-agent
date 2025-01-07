// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.Utilities
{
    public class EventSubscription<T> : IDisposable
    {
        private readonly Action<T> _callback;
        private readonly Func<Task<T>> _taskFunc;

        public EventSubscription(Action<T> callback)
        {
            _callback = callback;
            EventBus<T>.Subscribe(_callback);
        }

        public EventSubscription(Func<Task<T>> taskFunc)
        {
            _taskFunc = taskFunc;
            EventBus<T>.Subscribe(_taskFunc);
        }

        public void Dispose()
        {
            EventBus<T>.Unsubscribe(_callback);
        }
    }
}
