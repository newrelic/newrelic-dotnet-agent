// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Web.Http.ExceptionHandling;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.WebApi2
{
    public class ExceptionLogger : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var version = method.Type.Assembly.GetName().Version;
            if (version == null)
                return new CanWrapResponse(false);

            var canWrap = method.MatchesAny(assemblyName: "System.Web.Http", typeName: "System.Web.Http.ExceptionHandling.CompositeExceptionLogger", methodName: "LogAsync") &&
                version.Major >= 5; // WebApi v2 == System.Web.Http v5
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var exceptionLoggerContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<ExceptionLoggerContext>(0);
            if (exceptionLoggerContext == null)
                return Delegates.NoOp;

            var exception = exceptionLoggerContext.Exception;
            if (exception == null)
                return Delegates.NoOp;

            transaction.NoticeError(exception);

            return Delegates.NoOp;
        }
    }
}
