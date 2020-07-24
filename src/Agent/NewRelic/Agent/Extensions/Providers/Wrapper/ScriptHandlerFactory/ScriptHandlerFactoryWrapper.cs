using System;
using System.Web;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.ScriptHandlerFactory
{
    public class ScriptHandlerFactoryWrapper : IWrapper
    {
        // these must be lazily instatiated when the wrapper is actually used, not when the wrapper is first instantiated, so they sit in a nested class
        private static class Statics
        {
            [NotNull]
            public static readonly Func<Object, IHttpHandler> GetSyncHandlerOriginalHandler = VisibilityBypasser.Instance.GenerateFieldAccessor<IHttpHandler>(AssemblyName, SyncTypeName, "_originalHandler");

            [NotNull]
            public static readonly Func<Object, IHttpHandler> GetAsyncHandlerOriginalHandler = VisibilityBypasser.Instance.GenerateFieldAccessor<IHttpHandler>(AssemblyName, AsyncTypeName, "_originalHandler");
        }


        private const String AssemblyName = "System.Web.Extensions";
        private const String SyncTypeName = "System.Web.Script.Services.ScriptHandlerFactory+HandlerWrapper";
        private const String AsyncTypeName = "System.Web.Script.Services.ScriptHandlerFactory+AsyncHandlerWrapper";
        private const String SyncMethodName = "ProcessRequest";
        private const String AsyncBeginMethodName = "BeginProcessRequest";

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

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
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

        [CanBeNull]
        private static IHttpHandler TryGetOriginalHandler([NotNull] String methodName, [NotNull] Object invocationTarget)
        {
            if (methodName == SyncMethodName)
                return Statics.GetSyncHandlerOriginalHandler(invocationTarget);

            if (methodName == AsyncBeginMethodName)
                return Statics.GetAsyncHandlerOriginalHandler(invocationTarget);

            throw new Exception("Unexpected instrumented method in wrapper: " + methodName);
        }

    }
}
