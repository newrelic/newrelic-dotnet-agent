/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Web;
using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.Storage.TransactionContext
{
    /// <summary>
    /// ASP.NET transaction context backed by HttpContext.  Will correctly follow web requests across threads and deal with mid-thread interuption.
    /// </summary>
    public class AspTransactionContext<T> : IContextStorage<T>
    {
        private readonly string _key;

        /// <summary>
        /// Dude.
        /// </summary>
        public AspTransactionContext(string key)
        {
            _key = key;
        }

        byte IContextStorage<T>.Priority { get { return 10; } }

        bool IContextStorage<T>.CanProvide { get { return HttpContext.Current != null; } }

        T IContextStorage<T>.GetData()
        {

            var httpContext = HttpContext.Current;
            if (httpContext == null)
                return default;

            return (T)httpContext.Items[_key];
        }

        void IContextStorage<T>.SetData(T value)
        {
            var httpContext = HttpContext.Current;
            if (httpContext == null)
                return;

            httpContext.Items[_key] = value;
        }

        void IContextStorage<T>.Clear()
        {
            HttpContext.Current?.Items?.Remove(_key);
        }
    }
}
