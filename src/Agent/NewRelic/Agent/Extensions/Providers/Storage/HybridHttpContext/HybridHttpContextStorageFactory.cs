// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Web.Hosting;
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
                    return IsHostedWebApp();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public ContextStorageType Type => ContextStorageType.HttpContext; // same type category as HttpContext

        public IContextStorage<T> CreateContext<T>(string key)
        {
            return new HybridHttpContextStorage<T>(key);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsHostedWebApp()
        {
            return HostingEnvironment.IsHosted; // only returns true if running in a web application
        }
    }
}
