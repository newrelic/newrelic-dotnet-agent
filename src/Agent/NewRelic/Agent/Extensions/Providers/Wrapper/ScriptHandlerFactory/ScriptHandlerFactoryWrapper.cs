// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using System.Web;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.ScriptHandlerFactory
{
    public class ScriptHandlerFactoryWrapper : IWrapper
    {
        // these must be lazily instatiated when the wrapper is actually used, not when the wrapper is first instantiated, so they sit in a nested class
        private static class Statics
        {
            public static readonly Func<object, IHttpHandler> GetSyncHandlerOriginalHandler = VisibilityBypasser.Instance.GenerateFieldReadAccessor<IHttpHandler>(AssemblyName, SyncTypeName, "_originalHandler");

            public static readonly Func<object, IHttpHandler> GetAsyncHandlerOriginalHandler = VisibilityBypasser.Instance.GenerateFieldReadAccessor<IHttpHandler>(AssemblyName, AsyncTypeName, "_originalHandler");
        }


        private const string AssemblyName = "System.Web.Extensions";
        private const string SyncTypeName = "System.Web.Script.Services.ScriptHandlerFactory+HandlerWrapper";
        private const string AsyncTypeName = "System.Web.Script.Services.ScriptHandlerFactory+AsyncHandlerWrapper";
        private const string SyncMethodName = "ProcessRequest";
        private const string AsyncBeginMethodName = "BeginProcessRequest";

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: AssemblyName, typeName: SyncTypeName, methodName: SyncMethodName, parameterSignature: "System.Web.HttpContext");
            if (!canWrap)
            {
                canWrap = method.MatchesAny(assemblyName: AssemblyName, typeName: AsyncTypeName, methodName: AsyncBeginMethodName);
            }

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var handler = instrumentedMethodCall.MethodCall.InvocationTarget;

            var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;
            var originalHandler = TryGetOriginalHandler(methodName, handler);
            if (originalHandler == null)
            {
                throw new Exception("Unable to create metric name for instrumentation of ScriptHandlerFactory.");
            }

            var typeName = originalHandler.GetType().ToString();
            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, typeName, methodName);
            return Delegates.GetDelegateFor(segment);
        }

        private static IHttpHandler TryGetOriginalHandler(string methodName, object invocationTarget)
        {
            if (methodName == SyncMethodName)
                return Statics.GetSyncHandlerOriginalHandler(invocationTarget);

            if (methodName == AsyncBeginMethodName)
                return Statics.GetAsyncHandlerOriginalHandler(invocationTarget);

            throw new Exception("Unexpected instrumented method in wrapper: " + methodName);
        }

    }
}
