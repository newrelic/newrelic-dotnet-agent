// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.SystemExtensions;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.OpenRasta
{
    public class GetHandlerWrapper : IWrapper
    {
        private const string TypeName = "OpenRasta.Hosting.AspNet.OpenRastaHandler";
        private const string MethodName = "GetHandler";

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "OpenRasta.Hosting.AspNet", typeName: TypeName, methodName: MethodName);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var httpContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<System.Web.HttpContext>(0);
            var url = httpContext.Request.RawUrl; // Do not use Request.Url. With OpenRasta, Request.Url is rewritten to something like /ignoreme.rastahook which does not reflect the actual request.

            //Handler name - much like the controller name from ASP .NET MVC / Web API
            var urlWithoutQueryString = StringsHelper.CleanUri(url);
            var handlerName = urlWithoutQueryString.Substring(urlWithoutQueryString.LastIndexOf("/", StringComparison.InvariantCultureIgnoreCase) + 1);

            //Since Open Rasta uses convention based routing (i.e. HTTP verbs) there will be little deviation in this name
            //however we should still pull it out of the MethodArguments for consistency and future-proofing
            var action = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<string>(1);
            var actionName = action ?? instrumentedMethodCall.MethodCall.Method.MethodName;

            //Title casing actionName
            System.Globalization.TextInfo textInfo = new System.Globalization.CultureInfo("en-US", false).TextInfo;
            actionName = textInfo.ToLower(actionName);

            transaction.SetWebTransactionName(WebTransactionType.OpenRasta, $"{handlerName}/{actionName}", TransactionNamePriority.FrameworkHigh);

            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, handlerName, actionName);
            return segment == null ? Delegates.NoOp : Delegates.GetDelegateFor(segment);
        }
    }
}
