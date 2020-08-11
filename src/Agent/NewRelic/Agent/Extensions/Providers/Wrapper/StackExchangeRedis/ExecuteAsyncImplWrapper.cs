// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Threading.Tasks;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis
{
    public abstract class ExecuteAsyncImplWrapper : IWrapper
    {
        public abstract string AssemblyName { get; }

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(
                assemblyName: AssemblyName,
                typeName: Common.ConnectionMultiplexerTypeName,
                methodName: "ExecuteAsyncImpl"
            );

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var operation = Common.GetRedisCommand(instrumentedMethodCall.MethodCall, AssemblyName);
            var connectionInfo = Common.GetConnectionInfoFromConnectionMultiplexer(instrumentedMethodCall.MethodCall, AssemblyName);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, ParsedSqlStatement.FromOperation(DatastoreVendor.Redis, operation), connectionInfo);

            //We're not using Delegates.GetAsyncDelegateFor(agent, segment) because if an async redis call is made from an asp.net mvc action,
            //the continuation may not run until that mvc action has finished executing, or has yielded execution, because the synchronization context
            //will only allow one thread to execute at a time. To work around this limitation, since we don't need access to HttpContext in our continuation,
            //we can just provide the TaskContinuationOptions.HideScheduler flag so that we will use the default ThreadPool scheduler to schedule our
            //continuation. Using the ThreadPool scheduler allows our continuation to run without needing to wait for the mvc action to finish executing or
            //yielding its execution. We're not applying this change across the board, because we still need to better understand the impact of making this
            //change more broadly vs just fixing a known customer issue.

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.HideScheduler);
        }
    }

    public class RedisExecuteAsyncImplWrapper : ExecuteAsyncImplWrapper
    {
        public override string AssemblyName => Common.RedisAssemblyName;
    }

    public class RedisStrongNameExecuteAsyncImplWrapper : ExecuteAsyncImplWrapper
    {
        public override string AssemblyName => Common.RedisAssemblyStrongName;
    }
}
