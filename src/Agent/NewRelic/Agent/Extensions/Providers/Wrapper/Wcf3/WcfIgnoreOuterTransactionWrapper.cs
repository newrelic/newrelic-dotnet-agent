/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Wcf3
{
    public class WcfIgnoreOuterTransactionWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            // WCF 4
            if (method.MatchesAny(assemblyName: "System.ServiceModel.Activation", typeName: "System.ServiceModel.Activation.HostedHttpRequestAsyncResult",
                methodName: ".ctor", parameterSignature: "System.Web.HttpApplication,System.String,System.Boolean,System.Boolean,System.AsyncCallback,System.Object"))
                return new CanWrapResponse(true);

            // WCF 3
            var canWrap = method.MatchesAny(assemblyName: "System.ServiceModel", typeName: "System.ServiceModel.Activation.HostedHttpRequestAsyncResult",
                methodName: ".ctor", parameterSignature: "System.Web.HttpApplication,System.Boolean,System.AsyncCallback,System.Object");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (ShouldIgnoreTransaction(instrumentedMethodCall.MethodCall))
                agent.CurrentTransaction.Ignore();

            return Delegates.NoOp;
        }

        private static bool ShouldIgnoreTransaction(MethodCall methodCall)
        {
            // WCF 4
            if (methodCall.MethodArguments.Length == 6
                && methodCall.MethodArguments[2] is bool)
            {
                // return !flowContext
                return !(bool)methodCall.MethodArguments[2];
            }

            // WCF 3
            if (methodCall.MethodArguments.Length == 4
                && methodCall.MethodArguments[1] is bool)
            {
                // return !flowContext
                return !(bool)methodCall.MethodArguments[1];
            }

            // if we couldn't find the flow context (bug, new version of WCF, etc.) then don't ignore
            return false;
        }
    }
}
