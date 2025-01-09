// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core
{
    public abstract class Singleton<T>
    {
        private Lazy<Task<T>> _lazyInstance;

        protected Singleton(T instance)
        {
            // Set the default instance from the argument, where it might be picked up in
            // the course of executing CreateInstance.
            _lazyInstance = new Lazy<Task<T>>(() => Task.FromResult(instance));

            // Create the real instance we care about, perhaps referencing
            // the default instance in _instance.
            _lazyInstance = new Lazy<Task<T>>(CreateInstanceAsync);
        }

        public async Task<T> ExistingInstanceAsync() => await _lazyInstance.Value.ConfigureAwait(false);

        public void SetInstance(T instance)
        {
            _lazyInstance = new Lazy<Task<T>>(() => Task.FromResult(instance));
        }

        protected abstract Task<T> CreateInstanceAsync();
    }
}
