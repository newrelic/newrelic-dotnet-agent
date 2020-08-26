// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Mvc3
{
    public class SyncInvokeActionWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "System.Web.Mvc", typeName: "System.Web.Mvc.ControllerActionInvoker", methodName: "InvokeAction");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            transaction.AttachToAsync();
            var controllerContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<dynamic>(0);

            if (controllerContext != null)
            {
                var controllerName = MvcRouteNamingHelper.TryGetControllerNameFromObject(controllerContext);
                var actionName = MvcRouteNamingHelper.TryGetActionNameFromRouteParameters(instrumentedMethodCall.MethodCall, controllerContext.RouteData);

                var transactionName = string.Format("{0}/{1}", controllerName, actionName);
                transaction.SetWebTransactionName(WebTransactionType.MVC, transactionName, TransactionNamePriority.FrameworkHigh);

                var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, controllerName, actionName);

                if (segment != null)
                {
                    return Delegates.GetDelegateFor(segment);
                }
            }

            return Delegates.NoOp;
        }
    }
}
