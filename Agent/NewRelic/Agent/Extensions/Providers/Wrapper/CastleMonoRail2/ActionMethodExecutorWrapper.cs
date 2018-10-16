using System;
using System.Diagnostics;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.CastleMonoRail2
{
	public class ActionMethodExecutorWrapper : IWrapper
	{
		private const String AssemblyName = "Castle.MonoRail.Framework";
		private const String TypeName = "Castle.MonoRail.Framework.ControllerContext";
		private const String PropertyControllerName = "Name";
		private const String PropertyAction = "Action";

		public bool IsTransactionRequired => true;

		// these must be lazily instantiated when the wrapper is actually used, not when the wrapper is first instantiated, so they sit in a nested class
		private static class Statics
		{
			private static Func<Object, String> _propertyControllerName;
			private static Func<Object, String> _propertyActionName;

			[NotNull]
			public static readonly Func<Object, String> GetPropertyControllerName = AssignPropertyControllerName();

			[NotNull]
			public static readonly Func<Object, String> GetPropertyAction = AssignPropertyAction();

			private static Func<Object, String> AssignPropertyControllerName()
			{
				return _propertyControllerName ?? (_propertyControllerName = VisibilityBypasser.Instance.GeneratePropertyAccessor<String>(AssemblyName, TypeName, PropertyControllerName));
			}

			private static Func<Object, String> AssignPropertyAction()
			{
				return _propertyActionName ?? (_propertyActionName = VisibilityBypasser.Instance.GeneratePropertyAccessor<String>(AssemblyName, TypeName, PropertyAction));
			}
		}

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: AssemblyName, typeNames: new[] { "Castle.MonoRail.Framework.ActionMethodExecutor", "Castle.MonoRail.Framework.ActionMethodExecutorCompatible"}, methodName: "Execute");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
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

			transactionWrapperApi.SetWebTransactionName(WebTransactionType.MonoRail, $"{controllerName}.{actionName}", TransactionNamePriority.FrameworkLow);
			var segment = transactionWrapperApi.StartMethodSegment(instrumentedMethodCall.MethodCall, controllerName, actionName);

			return Delegates.GetDelegateFor(segment);
		}

		[CanBeNull]
		private static String TryGetPropertyName([NotNull] String propertyName, [NotNull] Object contextObject)
		{
			if (propertyName == PropertyControllerName)
				return Statics.GetPropertyControllerName(contextObject);
			if (propertyName == PropertyAction)
				return Statics.GetPropertyAction(contextObject);

			throw new Exception("Unexpected instrumented property in wrapper: " + contextObject + "." + propertyName);
		}
	}
}
