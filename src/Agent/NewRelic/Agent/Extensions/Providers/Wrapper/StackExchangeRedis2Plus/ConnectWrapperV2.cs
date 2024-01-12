// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using StackExchange.Redis;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis2Plus
{
    public class ConnectWrapperV2 : IWrapper
    {
        public bool IsTransactionRequired => false;

        private const string WrapperName = "stackexchangeredis-connect";
        private static readonly object _lock = new();

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, Agent.Api.ITransaction transaction)
        {
            return Delegates.GetDelegateFor<ConnectionMultiplexer>(
                onSuccess: RegisterProfiler);

            void RegisterProfiler(IConnectionMultiplexer multiplexer)
            {
                var xAgent = (IAgentExperimental)agent;

                // The SessionCache is not connection-specific.  This checks for an existing cache and creates one if there is none.
                if (xAgent.StackExchangeRedisCache == null)
                {
                    lock (_lock)
                    {
                        if (xAgent.StackExchangeRedisCache == null)
                        {
                            // We only need the hashcode since nothing will change for the methodCall
                            var hashCode = RuntimeHelpers.GetHashCode(multiplexer);
                            var sessionCache = new SessionCache(agent, hashCode);

                            xAgent.StackExchangeRedisCache = sessionCache;
                        }
                    }
                }

                // Registers the profiling function from the shared SessionCache.
                multiplexer.RegisterProfiler(((SessionCache)xAgent.StackExchangeRedisCache).GetProfilingSession());
            }
        }
    }
}
