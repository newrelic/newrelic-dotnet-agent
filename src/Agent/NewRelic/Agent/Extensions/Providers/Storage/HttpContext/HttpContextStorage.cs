// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.Storage.HttpContext
{
    /// <summary>
    /// ASP.NET transaction context backed by HttpContext.  Will correctly follow web requests 
    /// across threads and deal with mid-thread interuption.
    /// </summary>
    public class HttpContextStorage<T> : IContextStorage<T>
    {
        private readonly string _key;

        /// <summary>
        /// Dude.
        /// </summary>
        public HttpContextStorage(string key)
        {
            _key = key;
        }

        byte IContextStorage<T>.Priority => 10;

        bool IContextStorage<T>.CanProvide { get { return System.Web.HttpContext.Current != null; } }

        T IContextStorage<T>.GetData()
        {

            var httpContext = System.Web.HttpContext.Current;
            if (httpContext == null)
                return default(T);

            return (T)httpContext.Items[_key];
        }

        void IContextStorage<T>.SetData(T value)
        {
            var httpContext = System.Web.HttpContext.Current;
            if (httpContext == null)
                return;

            httpContext.Items[_key] = value;
        }

        void IContextStorage<T>.Clear()
        {
            System.Web.HttpContext.Current?.Items?.Remove(_key);
        }
    }
}
