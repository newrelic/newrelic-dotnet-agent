// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers;
using System;

namespace NewRelic.Providers.Storage.AsyncLocal
{
    public class AsyncLocalStorageFactory : IContextStorageFactory
    {
        public bool IsAsyncStorage => true;
        public bool IsHybridStorage => false;

        public string Name => GetType().FullName;

        public bool IsValid => true;

        public ContextStorageType Type => ContextStorageType.AsyncLocal;

        public IContextStorage<T> CreateContext<T>(string key)
        {
            return new AsyncLocalStorage<T>();
        }
    }
}
