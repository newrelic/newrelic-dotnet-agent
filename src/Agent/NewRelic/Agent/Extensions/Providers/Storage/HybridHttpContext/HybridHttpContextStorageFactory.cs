// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.Storage.HybridHttpContext
{
    public class HybridHttpContextStorageFactory : IContextStorageFactory
    {
        public bool IsAsyncStorage => false; // not really true, since HttpContext can flow with async calls, but we don't mark it as async storage
        public bool IsHybridStorage => true;

        public bool IsValid
        {
            get
            {
                try
                {
                    AccessHttpContextClass();
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            }
        }

        public ContextStorageType Type => ContextStorageType.HttpContext; // same type category as HttpContext

        public IContextStorage<T> CreateContext<T>(string key)
        {
            return new HybridHttpContextStorage<T>(key);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AccessHttpContextClass()
        {
            if (System.Web.HttpContext.Current == null)
                return;
        }
    }
}
