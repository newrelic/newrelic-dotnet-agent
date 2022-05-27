// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Mvc3
{
    public class InvokeExceptionFiltersWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "System.Web.Mvc", typeName: "System.Web.Mvc.ControllerActionInvoker", methodName: "InvokeExceptionFilters");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var exception = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<Exception>(2);
            if (exception == null)
            {
                // TODO: I think it would be valuable to capture any information at this point... when is this ever called with a null exception?
                return Delegates.NoOp;
            }

            transaction.NoticeError(exception);

            return Delegates.NoOp;
        }
    }
}
