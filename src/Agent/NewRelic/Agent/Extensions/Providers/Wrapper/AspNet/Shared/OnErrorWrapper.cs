// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.AspNet.Shared
{
    public class OnErrorWrapper : IWrapper
    {
        public const string WrapperName = "AspNet.OnErrorTracer";

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var canWrap = methodInfo.RequestedWrapperName.Equals(WrapperName, StringComparison.OrdinalIgnoreCase);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var exception = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<Exception>(0);
            if (exception == null)
                return Delegates.NoOp;

            transaction.NoticeError(exception);

            return Delegates.NoOp;
        }
    }
}
