// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Wcf3
{
    public class ServiceChannelProxyWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny
            (
                assemblyName: "System.ServiceModel",
                typeName: "System.ServiceModel.Channels.ServiceChannelProxy",
                methodName: "InvokeService"
            );
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var name = GetName(instrumentedMethodCall.MethodCall);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, name);

            return Delegates.GetDelegateFor(segment);
        }
        private static string GetName(MethodCall methodCall)
        {
            var methodCallMessage = methodCall.MethodArguments.ExtractAs<System.Runtime.Remoting.Messaging.IMethodCallMessage>(0);
            if (methodCallMessage == null)
                throw new NullReferenceException("methodCallMessage");

            var typeName = methodCallMessage.TypeName;
            if (typeName == null)
                throw new NullReferenceException("typeName");
            // the type name is the full class name followed by a comma and the assembly info.  We need to cut off at the comma
            typeName = typeName.TrimAfter(",");

            var methodName = methodCallMessage.MethodName;
            if (methodName == null)
                throw new NullReferenceException("methodName");

            return string.Format("{0}.{1}", typeName, methodName);
        }

    }
}
