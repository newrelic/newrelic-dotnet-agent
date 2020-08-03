// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.Remoting.Messaging;

namespace NewRelic.Providers.Storage.CallStack.AsyncLocal
{
    /// <summary>
    /// A simple implementation of AsyncLocal that works in .NET 4.5.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AsyncLocal<T>
    {
        private readonly string _key;

        public AsyncLocal(string key)
        {
            _key = key;
        }
        public T Value
        {
            get
            {
                var obj = CallContext.LogicalGetData(_key);
                return obj == null ? default : (T)obj;
            }
            set
            {
                CallContext.LogicalSetData(_key, value);
            }
        }
    }
}
