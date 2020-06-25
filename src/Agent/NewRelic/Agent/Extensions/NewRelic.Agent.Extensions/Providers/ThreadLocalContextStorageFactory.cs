/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Extensions.Providers
{
    /// <summary>
    /// Factory for creating an IContextStorage instance.
    /// </summary>
    public class ThreadLocalContextStorageFactory : IContextStorageFactory
    {
        public bool IsAsyncStorage => false;

        bool IContextStorageFactory.IsValid => true;

        ContextStorageType IContextStorageFactory.Type => ContextStorageType.ThreadLocal;

        IContextStorage<T> IContextStorageFactory.CreateContext<T>(string key)
        {
            return new ThreadLocalStorage<T>(key);
        }
    }
}
