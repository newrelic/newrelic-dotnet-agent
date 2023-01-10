// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using StackExchange.Redis;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis2Plus
{
    public class ConnectWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "stackexchangeredis-connect";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, Agent.Api.ITransaction transaction)
        {
            var multiplexer = instrumentedMethodCall.MethodCall.MethodArguments[0] as IConnectionMultiplexer;

            // We need this information to create a DataStoreSegemnt  - using a new method called StartStackExchangeRedisSegment
            var connection = Common.GetConnectionInfoFromConnectionMultiplexer(multiplexer, agent.Configuration.UtilizationHostName);
            var xAgent = (IAgentExperimental)agent;

            // The SessionCache is not connection-specific.  This checks for an existing cache and creates one if there is none.
            if (((IAgentExperimental)agent).StackExchangeRedisCache == null)
            {
                // We only need the hashcode since nothing will change for the methodCall
                var hashCode = RuntimeHelpers.GetHashCode(multiplexer);
                var sessionCache = new SessionCache(agent, connection, hashCode);
                xAgent.StackExchangeRedisCache = sessionCache;
            }

            // Registers the profiling function from the shared SessionCache.
            multiplexer.RegisterProfiler(((SessionCache)xAgent.StackExchangeRedisCache).GetProfilingSession());

            // We don't want a segment here since this is only preparing for later operations.
            return Delegates.NoOp;
        }
    }
}
