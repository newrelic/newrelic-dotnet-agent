using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.CastleMonoRail2
{
    public class ActionMethodExecutorWrapper : IWrapper
    {
        private const string AssemblyName = "Castle.MonoRail.Framework";
        private const string TypeName = "Castle.MonoRail.Framework.ControllerContext";
        private const string PropertyControllerName = "Name";
        private const string PropertyAction = "Action";

        public bool IsTransactionRequired => true;

        // these must be lazily instantiated when the wrapper is actually used, not when the wrapper is first instantiated, so they sit in a nested class
        private static class Statics
        {
            private static Func<object, string> _propertyControllerName;
            private static Func<object, string> _propertyActionName;
            public static readonly Func<object, string> GetPropertyControllerName = AssignPropertyControllerName();
            public static readonly Func<object, string> GetPropertyAction = AssignPropertyAction();

            private static Func<object, string> AssignPropertyControllerName()
            {
                return _propertyControllerName ?? (_propertyControllerName = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(AssemblyName, TypeName, PropertyControllerName));
            }

            private static Func<object, string> AssignPropertyAction()
            {
                return _propertyActionName ?? (_propertyActionName = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(AssemblyName, TypeName, PropertyAction));
            }
        }

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: AssemblyName, typeNames: new[] { "Castle.MonoRail.Framework.ActionMethodExecutor", "Castle.MonoRail.Framework.ActionMethodExecutorCompatible" }, methodName: "Execute");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var contextObject = instrumentedMethodCall.MethodCall.MethodArguments[2];
            if (contextObject == null)
                throw new NullReferenceException(nameof(contextObject));

            var controllerName = TryGetPropertyName(PropertyControllerName, contextObject);
            if (controllerName == null)
                throw new NullReferenceException(nameof(controllerName));

            var actionName = TryGetPropertyName(PropertyAction, contextObject);
            if (actionName == null)
                throw new NullReferenceException(nameof(actionName));

            transaction.SetWebTransactionName(WebTransactionType.MonoRail, $"{controllerName}.{actionName}", 5);
            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, controllerName, actionName);

            return Delegates.GetDelegateFor(segment);
        }

        private static string TryGetPropertyName(string propertyName, object contextObject)
        {
            if (propertyName == PropertyControllerName)
                return Statics.GetPropertyControllerName(contextObject);
            if (propertyName == PropertyAction)
                return Statics.GetPropertyAction(contextObject);

            throw new Exception("Unexpected instrumented property in wrapper: " + contextObject + "." + propertyName);
        }
    }
}
