/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Web;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.WebOptimization
{
    public class StaticResourceNamingWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "System.Web.Optimization", typeName: "System.Web.Optimization.BundleHandler", methodName: "ProcessRequest");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var httpContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<HttpContext>(0);
            var assetName = httpContext.Request.Path.TrimStart('/');

            transaction.SetWebTransactionName(WebTransactionType.ASP, assetName, 6);
            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, assetName);

            if (segment == null)
                return Delegates.NoOp;

            return Delegates.GetDelegateFor(segment);
        }
    }
}
