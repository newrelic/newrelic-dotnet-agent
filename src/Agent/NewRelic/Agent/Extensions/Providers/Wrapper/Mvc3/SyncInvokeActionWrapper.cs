// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

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
                // IMPORTANT: Resist the urge to blindly refactor all of this code to use `var`
                // IMPORTANT: We are being intentional with types over using `var` here due to
                // IMPORTANT: the effects of handling a `dynamic` object
                string controllerName = MvcRouteNamingHelper.TryGetControllerNameFromObject(controllerContext);
                string actionName = MvcRouteNamingHelper.TryGetActionNameFromRouteParameters(instrumentedMethodCall.MethodCall, controllerContext.RouteData);

                string transactionName = controllerName + "/" + actionName;
                transaction.SetWebTransactionName(WebTransactionType.MVC, transactionName, TransactionNamePriority.FrameworkLow);

                ISegment segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, controllerName, actionName);

                // segment should never be null.. it will either be a NoOp segment, or we would have thrown here...
                if (segment != null)
                {
                    string fullControllerName = MvcRouteNamingHelper.TryGetControllerFullNameFromObject(controllerContext);
                    if (fullControllerName != null)
                    {
                        ISegmentExperimental segmentApi = segment.GetExperimentalApi();
                        segmentApi.UserCodeNamespace = fullControllerName;
                        segmentApi.UserCodeFunction = actionName;
                    }

                    return Delegates.GetDelegateFor(segment);
                }
            }

            return Delegates.NoOp;
        }
    }
}
