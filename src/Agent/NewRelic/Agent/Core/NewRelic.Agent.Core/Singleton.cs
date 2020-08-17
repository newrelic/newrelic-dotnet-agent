// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core
{
    public abstract class Singleton<T>
    {
        private T _instance;

        protected Singleton(T instance)
        {
            // Set the default instance from the argument, where it might be picked up in
            // the course of executing CreateInstance.
            _instance = instance;

            // Create the real instance we care about, perhaps referencing
            // the default instance in _instance.
            _instance = CreateInstance();
        }

        public T ExistingInstance
        {
            get
            {
                return _instance;
            }
        }

        public void SetInstance(T instance)
        {
            _instance = instance;
        }

        protected abstract T CreateInstance();
    }
}
