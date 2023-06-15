// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NewRelic.Providers.Wrapper.Sql
{
    public class OpenConnectionWrapper : OpenConnectionWrapperBase
    {
        private static readonly string[] _tracerNames =
        {
            "OpenConnectionTracer",
            "OpenConnectionWrapper",
        };

        public override string[] WrapperNames => _tracerNames;
        public override bool ExecuteAsAsync => false;
    }

    public class OpenConnectionAsyncWrapper : OpenConnectionWrapperBase
    {
        private static readonly string[] _tracerNames =
        {
            "OpenConnectionTracerAsync",
            "OpenConnectionWrapperAsync"
        };
        public override string[] WrapperNames => _tracerNames;
        public override bool ExecuteAsAsync => true;
    }


    public abstract class OpenConnectionWrapperBase : IWrapper
    {
        public abstract string[] WrapperNames { get; }

        public abstract bool ExecuteAsAsync { get; }

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var canWrap = WrapperNames.Contains(methodInfo.RequestedWrapperName, StringComparer.OrdinalIgnoreCase);
            if (canWrap && ExecuteAsAsync)
            {
                var method = methodInfo.Method;
                return TaskFriendlySyncContextValidator.CanWrapAsyncMethod(method.Type.Assembly.GetName().Name, method.Type.FullName, method.MethodName);
            }

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var typeName = instrumentedMethodCall.MethodCall.Method.Type.FullName ?? "unknown";
            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, typeName, instrumentedMethodCall.MethodCall.Method.MethodName, isLeaf: true);

            return ExecuteAsAsync
                ? Delegates.GetAsyncDelegateFor<Task>(agent, segment)
                : Delegates.GetDelegateFor(segment);
        }
    }
}
