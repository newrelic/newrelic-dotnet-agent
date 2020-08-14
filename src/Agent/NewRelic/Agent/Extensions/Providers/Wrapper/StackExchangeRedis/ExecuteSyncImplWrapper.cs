// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis
{
    public abstract class ExecuteSyncImplWrapper : IWrapper
    {
        protected abstract string AssemblyName { get; }

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(
                assemblyName: AssemblyName,
                typeName: Common.ConnectionMultiplexerTypeName,
                methodName: "ExecuteSyncImpl"
            );

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var operation = Common.GetRedisCommand(instrumentedMethodCall.MethodCall, AssemblyName);
            var connectionInfo = Common.GetConnectionInfoFromConnectionMultiplexer(instrumentedMethodCall.MethodCall, AssemblyName);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, ParsedSqlStatement.FromOperation(DatastoreVendor.Redis, operation), connectionInfo);

            return Delegates.GetDelegateFor(segment);
        }
    }

    public class RedisExecuteSyncImplWrapper : ExecuteSyncImplWrapper
    {
        protected override string AssemblyName => Common.RedisAssemblyName;
    }

    public class RedisStrongNameExecuteSyncImplWrapper : ExecuteSyncImplWrapper
    {
        protected override string AssemblyName => Common.RedisAssemblyStrongName;
    }
}
