// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.WebServices
{
    public class WebServiceMethodWrapper : IWrapper
    {

        public Func<object, string> GetMethodInfo => _getMethodInfo ?? (_getMethodInfo = VisibilityBypasser.Instance.GenerateFieldAccessor<string>("System.Web.Extensions", "System.Web.Script.Services.WebServiceMethodData", "_methodName"));
        public Func<object, object> GetMethodOwner => _getMethodOwner ?? (_getMethodOwner = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("System.Web.Extensions", "System.Web.Script.Services.WebServiceMethodData", "Owner"));
        public Func<object, object> GetMethodTypeData => _getMethodTypeData ?? (_getMethodTypeData = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("System.Web.Extensions", "System.Web.Script.Services.WebServiceData", "TypeData"));
        public Func<object, Type> GetMethodType => _getMethodType ?? (_getMethodType = VisibilityBypasser.Instance.GeneratePropertyAccessor<Type>("System.Web.Extensions", "System.Web.Script.Services.WebServiceTypeData", "Type"));


        public bool IsTransactionRequired => true;
        private Func<object, string> _getMethodInfo;
        private Func<object, object> _getMethodOwner;
        private Func<object, object> _getMethodTypeData;
        private Func<object, Type> _getMethodType;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "System.Web.Extensions", typeName: "System.Web.Script.Services.WebServiceMethodData", methodName: "CallMethod");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
            IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var methodName = GetMethodInfo.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);
            if (methodName == null)
                throw new NullReferenceException("Could not retrieve a value from _methodName field on the invocation target");

            var service = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var serviceType = "";
            if (service == null)
            {
                var methodOwner = GetMethodOwner.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);
                var methodTypeData = GetMethodTypeData.Invoke(methodOwner);
                var methodType = GetMethodType.Invoke(methodTypeData);
                serviceType = methodType.Name;
            }
            else
            {
                serviceType = service.GetType().Name;
            }

            var transactionName = serviceType + "/" + methodName;

            transaction.SetWebTransactionName(WebTransactionType.WebService, transactionName, 5);
            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall,
                instrumentedMethodCall.MethodCall.Method.Type.ToString(), instrumentedMethodCall.MethodCall.Method.MethodName);
            return Delegates.GetDelegateFor(segment);
        }
    }
}

