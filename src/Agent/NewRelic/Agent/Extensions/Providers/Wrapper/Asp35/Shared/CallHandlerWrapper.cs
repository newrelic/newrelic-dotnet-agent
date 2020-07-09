using System;
using System.Web;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
	public class CallHandlerWrapper : IWrapper
	{
		[NotNull]
		public Func<Object, HttpApplication> GetHttpApplication { get { return _getHttpApplication ?? (_getHttpApplication = VisibilityBypasser.Instance.GenerateFieldAccessor<HttpApplication>("System.Web", "System.Web.HttpApplication+CallHandlerExecutionStep", "_application")); } }

		public bool IsTransactionRequired => true;

		[CanBeNull]
		private Func<Object, HttpApplication> _getHttpApplication;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Web", typeName: "System.Web.HttpApplication+CallHandlerExecutionStep", methodName: "System.Web.HttpApplication.IExecutionStep.Execute");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var httpApplication = GetHttpApplication(instrumentedMethodCall.MethodCall.InvocationTarget);
			if (httpApplication == null)
				throw new NullReferenceException("httpApplication");

			var httpContext = httpApplication.Context;
			if (httpContext == null)
				throw new NullReferenceException("httpContext");

			var httpHandler = httpContext.Handler;
			if (httpHandler == null)
				return Delegates.NoOp;

			var httpHandlerName = httpHandler.GetType().Name;
			transaction.SetWebTransactionName(WebTransactionType.ASP, httpHandlerName, 3);

			return Delegates.NoOp;
		}
	}
}
