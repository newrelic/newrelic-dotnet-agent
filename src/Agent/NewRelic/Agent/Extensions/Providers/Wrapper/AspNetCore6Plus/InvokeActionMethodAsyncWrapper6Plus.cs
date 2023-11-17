// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{
    public class InvokeActionMethodAsyncWrapper6Plus : IWrapper
    {
        private Func<object, ControllerContext> _getControllerContext;
        private Func<object, ControllerContext> GetControllerContext(string typeName) { return _getControllerContext ?? (_getControllerContext = VisibilityBypasser.Instance.GenerateFieldReadAccessor<ControllerContext>("Microsoft.AspNetCore.Mvc.Core", typeName, "_controllerContext")); }

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("InvokeActionMethodAsyncWrapper6Plus".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            //handle the .NetCore 3.0 case where the namespace is Infrastructure instead of Internal.
            var controllerContext = GetControllerContext(instrumentedMethodCall.MethodCall.Method.Type.FullName).Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);
            var actionDescriptor = controllerContext.ActionDescriptor;

            var transactionName = CreateTransactionName(actionDescriptor);

            transaction.SetWebTransactionName(WebTransactionType.MVC, transactionName, TransactionNamePriority.FrameworkHigh);

            var controllerTypeInfo = controllerContext.ActionDescriptor.ControllerTypeInfo;
            //Framework uses ControllerType.Action for these metrics & transactions. WebApi is Controller.Action for both
            //Taking opinionated stance to do ControllerType.MethodName for segments. Controller/Action for transactions
            var controllerTypeName = controllerTypeInfo.Name;
            var methodName = controllerContext.ActionDescriptor.MethodInfo.Name;

            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, controllerTypeName, methodName);

            var segmentApi = segment.GetExperimentalApi();
            segmentApi.UserCodeNamespace = controllerTypeInfo.FullName;
            segmentApi.UserCodeFunction = methodName;

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, TaskContinueWithOption.None);
        }

        private static string CreateTransactionName(ControllerActionDescriptor actionDescriptor)
        {
            var controllerName = actionDescriptor.ControllerName;
            var actionName = actionDescriptor.ActionName;

            var transactionName = $"{controllerName}/{actionName}";

            foreach (var parameter in actionDescriptor.Parameters)
            {
                transactionName += "/{" + parameter.Name + "}";
            }

            return transactionName;
        }
    }
}
