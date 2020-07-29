/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Web.UI;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
    public class AspPagesTransactionNameWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;

            var canWrap = method.MatchesAny(assemblyName: "System.Web", typeName: "System.Web.UI.Page", methodNames: new[] { "ProcessRequest", "AsyncPageBeginProcessRequest" });
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var page = instrumentedMethodCall.MethodCall.InvocationTarget as Page;
            if (page == null)
                return Delegates.NoOp;

            var pagePath = page.AppRelativeVirtualPath;
            if (pagePath == null)
                return Delegates.NoOp;

            if (pagePath.StartsWith("~/"))
                pagePath = pagePath.Substring(2);

            pagePath = pagePath.ToLower();

            transaction.SetWebTransactionName(WebTransactionType.ASP, pagePath, 6);
            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, pagePath);

            return Delegates.GetDelegateFor(segment);
        }
    }
}
