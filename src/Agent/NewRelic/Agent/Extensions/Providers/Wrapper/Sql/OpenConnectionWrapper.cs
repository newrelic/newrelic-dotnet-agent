// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Linq;

namespace NewRelic.Providers.Wrapper.Sql
{
    public class OpenConnectionWrapper : IWrapper
    {
        public static readonly string[] WrapperNames =
        {
            "OpenConnectionTracer",
            "OpenConnectionWrapper"
        };

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperNames.Contains(methodInfo.RequestedWrapperName, StringComparer.OrdinalIgnoreCase));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var typeName = instrumentedMethodCall.MethodCall.Method.Type.FullName ?? "unknown";
            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, typeName, instrumentedMethodCall.MethodCall.Method.MethodName, isLeaf: true);

            return Delegates.GetDelegateFor(segment);
        }
    }
}
