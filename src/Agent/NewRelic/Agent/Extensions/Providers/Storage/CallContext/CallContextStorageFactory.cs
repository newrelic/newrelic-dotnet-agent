// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.Storage.CallContext
{
    public class CallContextStorageFactory : IContextStorageFactory
    {
        //Not searching inheritance tree for now to avoid any additional perf penalty
        private const bool ShouldSearchParentsForAttribute = false;

        public bool IsAsyncStorage => true;
        public bool IsHybridStorage => false;
        public bool IsValid => true;
        public ContextStorageType Type => ContextStorageType.CallContextLogicalData;

        public IContextStorage<T> CreateContext<T>(string key)
        {
            if (TypeNeedsSerializableContainer<T>())
            {
                return new CallContextWrappedStorage<T>(key);
            }
            else
            {
                return new CallContextStorage<T>(key);
            }
        }

        private static bool TypeNeedsSerializableContainer<T>()
        {
            return typeof(T).IsDefined(typeof(NeedSerializableContainer), ShouldSearchParentsForAttribute);
        }
    }
}
