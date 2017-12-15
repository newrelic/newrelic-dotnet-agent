using System;
using System.Web.Mvc;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Mvc3
{
	public class SyncInvokeActionWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Web.Mvc", typeName: "System.Web.Mvc.ControllerActionInvoker", methodName: "InvokeAction");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var controllerContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<ControllerContext>(0);
			var controllerName = MvcRouteNamingHelper.TryGetControllerNameFromObject(controllerContext);
			var actionName = MvcRouteNamingHelper.TryGetActionNameFromRouteParameters(instrumentedMethodCall.MethodCall, controllerContext.RouteData);

			var transactionName = String.Format("{0}/{1}", controllerName, actionName);
			transaction.SetWebTransactionName(WebTransactionType.MVC, transactionName, 6);
			
			var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, controllerName, actionName);
			if (segment == null)
				return Delegates.NoOp;

			return Delegates.GetDelegateFor(segment);
		}
	}
}
