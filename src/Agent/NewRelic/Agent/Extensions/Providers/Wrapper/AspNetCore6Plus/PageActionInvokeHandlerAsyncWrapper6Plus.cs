// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{
    public class PageActionInvokeHandlerAsyncWrapper6Plus : IWrapper
    {
        private static Func<object, PageContext> _getPageContext;

        static PageActionInvokeHandlerAsyncWrapper6Plus()
        {
            _getPageContext = VisibilityBypasser.Instance.GenerateFieldReadAccessor<PageContext>("Microsoft.AspNetCore.Mvc.RazorPages",
                "Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure.PageActionInvoker", "_pageContext");
        }

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("PageActionInvokeHandlerAsyncWrapper6Plus".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var pageContext = _getPageContext(instrumentedMethodCall.MethodCall.InvocationTarget);

            var actionDescriptor = pageContext.ActionDescriptor;

            var transactionName = CreateTransactionName(actionDescriptor);

            transaction.SetWebTransactionName(WebTransactionType.Razor, transactionName, TransactionNamePriority.FrameworkHigh);

            var actionDescriptorPageTypeInfo = actionDescriptor.PageTypeInfo;
            var pageTypeName = actionDescriptorPageTypeInfo.Name;
            var handlerMethodName = actionDescriptor.HandlerMethods.SingleOrDefault(m =>
                m.HttpMethod.Equals(pageContext.HttpContext.Request.Method, StringComparison.CurrentCultureIgnoreCase))?.MethodInfo.Name;

            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, pageTypeName, handlerMethodName ?? pageContext.HttpContext.Request.Method);


            var segmentApi = segment.GetExperimentalApi();
            segmentApi.UserCodeNamespace = actionDescriptorPageTypeInfo.FullName;
            segmentApi.UserCodeFunction = handlerMethodName ?? "<unknown>";

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, TaskContinueWithOption.None);
        }

        private static string CreateTransactionName(CompiledPageActionDescriptor actionDescriptor)
        {
            var transactionName = $"Pages{actionDescriptor.DisplayName}";

            return transactionName;
        }
    }
}
