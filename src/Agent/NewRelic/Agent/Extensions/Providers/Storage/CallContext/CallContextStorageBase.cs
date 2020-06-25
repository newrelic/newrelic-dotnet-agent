/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.Storage.CallContext
{
    public abstract class CallContextStorageBase<T> : IContextStorage<T>
    {
        public byte Priority => 1;
        public bool CanProvide => true;

        public abstract void Clear();
        public abstract T GetData();
        public abstract void SetData(T value);
    }
}
