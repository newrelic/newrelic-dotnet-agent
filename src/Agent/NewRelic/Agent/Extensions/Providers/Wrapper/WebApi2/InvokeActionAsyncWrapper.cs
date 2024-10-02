// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.WebApi2
{
    public class AsyncApiControllerActionInvoker : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;

            var version = method.Type.Assembly.GetName().Version;
            if (version == null)
                return new CanWrapResponse(false);

            var canWrap = method.MatchesAny(assemblyName: "System.Web.Http", typeName: "System.Web.Http.Controllers.ApiControllerActionInvoker", methodName: "InvokeActionAsync") &&
                version.Major >= 5; // WebApi v2 == System.Web.Http v5

            if (canWrap)
            {
                return TaskFriendlySyncContextValidator.CanWrapAsyncMethod("System.Web.Http", "System.Web.Http.Controllers.ApiControllerActionInvoker", method.MethodName);
            }

            return new CanWrapResponse(false);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var httpActionContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<HttpActionContext>(0);
            var controllerDescriptor = TryGetControllerDescriptor(httpActionContext);

            var controllerName = controllerDescriptor?.ControllerName ?? "Unknown Controller";
            var actionName = TryGetActionName(httpActionContext) ?? "Unknown Action";

            var transactionName = string.Format("{0}/{1}", controllerName, actionName);
            transaction.SetWebTransactionName(WebTransactionType.WebAPI, transactionName, TransactionNamePriority.FrameworkHigh);

            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, controllerName, actionName);

            var segmentApi = segment.GetExperimentalApi();
            segmentApi.UserCodeNamespace = controllerDescriptor?.ControllerType.FullName;
            segmentApi.UserCodeFunction = httpActionContext.ActionDescriptor?.ActionName;

            return Delegates.GetAsyncDelegateFor<Task<HttpResponseMessage>>(agent, segment);
        }

        private static HttpControllerDescriptor TryGetControllerDescriptor(HttpActionContext httpActionContext)
        {
            var controllerContext = httpActionContext.ControllerContext;
            if (controllerContext == null)
                return null;

            return controllerContext.ControllerDescriptor;
        }

        private static string TryGetActionName(HttpActionContext httpActionContext)
        {
            var actionDescriptor = httpActionContext.ActionDescriptor;
            if (actionDescriptor == null)
                return null;

            return actionDescriptor.ActionName;
        }
    }
}
