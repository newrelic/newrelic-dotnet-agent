// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace NewRelic.Agent.Core
{
    /// <summary>
    /// Cache of the Agent methods that can be invoked by the byte-code injected by the profiler.
    /// </summary>
    public class ProfilerAgentMethodCallCache
    {
        private static readonly ConcurrentDictionary<string, MethodInfo> _methodInfoCache = new ConcurrentDictionary<string, MethodInfo>();

        /// <summary>
        /// This method is invoked once using reflection by the byte-code injected by the profiler. This method is used to get a 
        /// reference to the method that is used to cache reflection lookups for Agent methods. These methods are either
        /// Public Agent API methods, or AgentShim methods.
        /// </summary>
        /// <returns>
        /// A reference to the method that can access the method info cache. The result is treated as an object
        /// to simplify the type definition injected by the profiler to store this reference.
        /// </returns>
        public static object GetMethodCacheFunc()
        {
            return (Func<string, string, string, Type[], MethodInfo>)GetAgentMethodInfoFromCache;
        }

        private static MethodInfo GetAgentMethodInfoFromCache(string key, string className, string methodName, Type[] types)
        {
            var methodInfoFactory = new MethodInfoCacheItemFactory(className, methodName, types);
            return _methodInfoCache!.GetOrAdd(key, methodInfoFactory.GetMethodInfo);
        }

        // Using a struct to rely to take advantage of stack allocation to reduce overall allocations.
        private struct MethodInfoCacheItemFactory
        {
            private string _className;
            private string _methodName;
            private Type[] _types;

            public MethodInfoCacheItemFactory(string className, string methodName, Type[] types)
            {
                _className = className;
                _methodName = methodName;
                _types = types;
            }

            public MethodInfo GetMethodInfo(string _)
            {
                var type = Type.GetType(_className);

                var method = _types != null ? type.GetMethod(_methodName, _types) : type.GetMethod(_methodName);

                return method;
            }
        }
    }
}
