// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.Storage.OperationContext
{
    /// <summary>
    /// WCF 3 context backed by OperationContext.
    /// </summary>
    public class OperationContextStorage<T> : IContextStorage<T>
    {
        private readonly string _key;

        /// <summary>
        /// 
        /// </summary>
        public OperationContextStorage(string key)
        {
            _key = key;
        }

        byte IContextStorage<T>.Priority { get { return 5; } }

        bool IContextStorage<T>.CanProvide { get { return OperationContextExtension.CanProvide; } }

        T IContextStorage<T>.GetData()
        {
            var currentOperationContext = OperationContextExtension.Current;
            if (currentOperationContext == null)
                return default(T);

            return (T)currentOperationContext.Items[_key];
        }

        void IContextStorage<T>.SetData(T value)
        {
            var currentOperationContext = OperationContextExtension.Current;
            if (currentOperationContext == null)
                return;

            currentOperationContext.Items[_key] = value;
        }

        void IContextStorage<T>.Clear()
        {
            OperationContextExtension.Current?.Items?.Remove(_key);
        }
    }
}
